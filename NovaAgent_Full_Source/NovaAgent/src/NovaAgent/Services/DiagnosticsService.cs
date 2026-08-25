using System.Runtime.InteropServices;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class DiagnosticsService
{
    private readonly SettingsService _settings;

    public DiagnosticsService(SettingsService settings) => _settings = settings;

    public Task<IReadOnlyList<DiagnosticCheck>> RunAsync(AppSettings settings) => Task.Run(() =>
    {
        var checks = new List<DiagnosticCheck>();
        var server = _settings.ResolveAppPath(settings.WhisperServerPath);
        var model = _settings.ResolveAppPath(settings.WhisperModelPath);

        var version = typeof(DiagnosticsService).Assembly.GetName().Version;
        checks.Add(new("Application", true,
            $"Nova Agent {version?.ToString(3) ?? "unknown"} | .NET {Environment.Version} | {RuntimeInformation.ProcessArchitecture}"));

        checks.Add(new("Operating system", OperatingSystem.IsWindowsVersionAtLeast(10),
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            "Nova Agent requires Windows 10 or newer."));

        checks.Add(new("Whisper server", File.Exists(server),
            File.Exists(server) ? server : $"Missing: {server}",
            "Run scripts\\setup-whisper.ps1 from the project root."));

        checks.Add(new("Whisper model", File.Exists(model),
            File.Exists(model) ? $"{model} ({new FileInfo(model).Length / 1024 / 1024:N0} MB)" : $"Missing: {model}",
            "Run scripts\\setup-whisper.ps1 or select an existing multilingual model."));

        IReadOnlyList<(int Number, string Name)> microphones;
        try { microphones = AudioCaptureService.GetInputDevices(); }
        catch { microphones = []; }
        var selectedMic = microphones.FirstOrDefault(m => m.Number == settings.MicrophoneDeviceNumber);
        checks.Add(new("Microphone", microphones.Count > 0 && selectedMic.Name is not null,
            microphones.Count == 0 ? "No recording device detected." :
            selectedMic.Name is null ? $"Device #{settings.MicrophoneDeviceNumber} is unavailable." : selectedMic.Name,
            "Connect or select a microphone, then restart Nova Agent."));

        try
        {
            var dataRoot = Path.GetPathRoot(_settings.DataDirectory) ?? _settings.DataDirectory;
            var drive = new DriveInfo(dataRoot);
            checks.Add(new("Free disk space", drive.AvailableFreeSpace >= 1024L * 1024 * 1024,
                $"{drive.AvailableFreeSpace / 1024d / 1024 / 1024:N1} GB available on {drive.Name}",
                "Keep at least 1 GB free for models, audio buffers, and logs."));
        }
        catch (Exception ex)
        {
            checks.Add(new("Free disk space", false, ex.Message,
                "Check access to the local application-data drive."));
        }

        var dataWritable = CanWriteDataDirectory(out var dataMessage);
        checks.Add(new("Local data access", dataWritable, dataMessage,
            "Allow Nova Agent to write to the current user's application-data folder."));

        checks.Add(new("Settings", true, _settings.SettingsPath));
        checks.Add(new("Privacy", true, "Speech stays local; Whisper binds to 127.0.0.1."));
        return (IReadOnlyList<DiagnosticCheck>)checks;
    });

    private bool CanWriteDataDirectory(out string message)
    {
        try
        {
            Directory.CreateDirectory(_settings.DataDirectory);
            var probe = Path.Combine(_settings.DataDirectory, $".write-test-{Guid.NewGuid():N}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            message = _settings.DataDirectory;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }
}
