using System.Text.Json;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class SettingsService
{
    private readonly object _sync = new();
    private readonly string _dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaAgent");
    private AppSettings? _cached;

    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SettingsPath => Path.Combine(_dir, "settings.json");
    public string DataDirectory => _dir;

    public AppSettings Load()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return Clone(_cached);

            Directory.CreateDirectory(_dir);

            if (!File.Exists(SettingsPath))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return Clone(defaults);
            }

            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(
                                   File.ReadAllText(SettingsPath), _json)
                               ?? new AppSettings();
                _cached = Normalize(settings);
                return Clone(_cached);
            }
            catch (Exception ex)
            {
                var recovery = Path.Combine(_dir,
                    $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try { File.Move(SettingsPath, recovery, overwrite: false); }
                catch (Exception backupError)
                {
                    AppLog.Error("Invalid settings backup could not be created.", backupError);
                }

                AppLog.Error($"Settings were invalid; defaults restored. Backup: {recovery}", ex);
                var defaults = new AppSettings();
                Save(defaults);
                return Clone(defaults);
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_dir);
            settings = Normalize(Clone(settings));

            var temporary = SettingsPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, _json);
            using (var stream = new FileStream(
                       temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, SettingsPath, overwrite: true);
            _cached = Clone(settings);
        }
    }

    public void Export(string destination, AppSettings settings) =>
        File.WriteAllText(destination, JsonSerializer.Serialize(Normalize(Clone(settings)), _json));

    public AppSettings Import(string source)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(source), _json)
                       ?? throw new InvalidDataException("The selected settings file is empty or invalid.");
        settings = Normalize(settings);
        Save(settings);
        return settings;
    }

    public string ResolveAppPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"These settings require a newer Nova Agent version (schema {settings.SchemaVersion}).");

        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        settings.ChunkMilliseconds = Math.Clamp(settings.ChunkMilliseconds, 2000, 10000);
        settings.ConversationSeconds = Math.Clamp(settings.ConversationSeconds, 8, 120);
        settings.MinimumPeak = Math.Clamp(settings.MinimumPeak, 0.001f, 0.5f);
        settings.WhisperPort = Math.Clamp(settings.WhisperPort, 1024, 65535);
        settings.MicrophoneDeviceNumber = Math.Max(0, settings.MicrophoneDeviceNumber);
        settings.SpeechRate = Math.Clamp(settings.SpeechRate, -10, 10);
        settings.SpeechVolume = Math.Clamp(settings.SpeechVolume, 0, 100);
        settings.FileSearchLimit = Math.Clamp(settings.FileSearchLimit, 1000, 100000);
        settings.WakeWords = settings.WakeWords?
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (settings.WakeWords.Length == 0)
            settings.WakeWords = ["nova", "নোভা", "hey nova"];
        settings.CustomApps = new Dictionary<string, string>(
            settings.CustomApps ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        return settings;
    }

    private static AppSettings Clone(AppSettings source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        AlwaysListening = source.AlwaysListening,
        RequireWakeWord = source.RequireWakeWord,
        SpeakResponses = source.SpeakResponses,
        AutoStart = source.AutoStart,
        CloseToTray = source.CloseToTray,
        ChunkMilliseconds = source.ChunkMilliseconds,
        ConversationSeconds = source.ConversationSeconds,
        MinimumPeak = source.MinimumPeak,
        WhisperPort = source.WhisperPort,
        MicrophoneDeviceNumber = source.MicrophoneDeviceNumber,
        SpeechRate = source.SpeechRate,
        SpeechVolume = source.SpeechVolume,
        FileSearchLimit = source.FileSearchLimit,
        WakeWords = source.WakeWords?.ToArray() ?? [],
        WhisperServerPath = source.WhisperServerPath,
        WhisperModelPath = source.WhisperModelPath,
        CustomApps = new Dictionary<string, string>(
            source.CustomApps ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase)
    };
}
