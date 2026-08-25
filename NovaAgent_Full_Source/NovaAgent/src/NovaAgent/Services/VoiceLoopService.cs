using System.Text.RegularExpressions;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class VoiceLoopService
{
    private readonly SettingsService _settingsService;
    private readonly AudioCaptureService _audio;
    private readonly WhisperService _whisper;
    private readonly SpeechOutputService _speech;
    private readonly CommandProcessor _commands;
    private readonly HistoryService _history;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private DateTime _conversationUntilUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _singleCapture = new(1, 1);

    public bool IsRunning => _loopTask is { IsCompleted: false };

    public event Action<string>? StatusChanged;
    public event Action<string>? TranscriptReceived;
    public event Action<CommandResult>? CommandCompleted;

    public VoiceLoopService(
        SettingsService settingsService,
        AudioCaptureService audio,
        WhisperService whisper,
        SpeechOutputService speech,
        CommandProcessor commands,
        HistoryService history)
    {
        _settingsService = settingsService;
        _audio = audio;
        _whisper = whisper;
        _speech = speech;
        _commands = commands;
        _history = history;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        var settings = _settingsService.Load();
        _speech.Configure(settings);

        _cts = new CancellationTokenSource();

        StatusChanged?.Invoke("Starting local voice engine...");
        var ready = await _whisper.EnsureServerAsync(settings, _cts.Token);
        if (!ready.Ok)
        {
            StatusChanged?.Invoke(ready.Message);
            _cts.Dispose();
            _cts = null;
            return;
        }

        StatusChanged?.Invoke(settings.RequireWakeWord
            ? $"Listening for: {string.Join(", ", settings.WakeWords)}"
            : "Listening continuously");

        _loopTask = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        _cts.Cancel();

        try
        {
            if (_loopTask is not null)
                await _loopTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
            StatusChanged?.Invoke("Stopped");
        }
    }

    public async Task ListenOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!await _singleCapture.WaitAsync(0, cancellationToken))
        {
            StatusChanged?.Invoke("The microphone is already in use.");
            return;
        }

        var settings = _settingsService.Load();
        _speech.Configure(settings);

        try
        {
            var ready = await _whisper.EnsureServerAsync(settings, cancellationToken);
            if (!ready.Ok)
            {
                StatusChanged?.Invoke(ready.Message);
                return;
            }

            StatusChanged?.Invoke("Listening once...");
            var (file, peak) = await _audio.CaptureAsync(
                Math.Max(5000, settings.ChunkMilliseconds),
                settings.MicrophoneDeviceNumber,
                cancellationToken);

            try
            {
                if (peak < settings.MinimumPeak)
                {
                    StatusChanged?.Invoke("No clear speech detected.");
                    return;
                }

                var text = await _whisper.TranscribeAsync(file, settings, cancellationToken);
                await HandleTranscriptAsync(text, settings, bypassWakeWord: true);
            }
            finally
            {
                AudioCaptureService.TryDelete(file);
                StatusChanged?.Invoke("Ready");
            }
        }
        finally
        {
            _singleCapture.Release();
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var consecutiveErrors = 0;
        while (!ct.IsCancellationRequested)
        {
            var settings = _settingsService.Load();
            _speech.Configure(settings);

            if (_speech.IsSpeaking)
            {
                await Task.Delay(250, ct);
                continue;
            }

            string? file = null;
            try
            {
                StatusChanged?.Invoke(
                    settings.RequireWakeWord && DateTime.UtcNow > _conversationUntilUtc
                        ? "Listening for wake word..."
                        : "Conversation active...");

                await _singleCapture.WaitAsync(ct);
                (string FilePath, float Peak) captured;
                try
                {
                    captured = await _audio.CaptureAsync(
                        settings.ChunkMilliseconds,
                        settings.MicrophoneDeviceNumber,
                        ct);
                    file = captured.FilePath;
                }
                finally
                {
                    _singleCapture.Release();
                }

                if (captured.Peak < settings.MinimumPeak)
                    continue;

                var text = await _whisper.TranscribeAsync(file, settings, ct);

                if (!string.IsNullOrWhiteSpace(text))
                    await HandleTranscriptAsync(text, settings, bypassWakeWord: false);

                consecutiveErrors = 0;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("Voice loop failed.", ex);
                consecutiveErrors = Math.Min(consecutiveErrors + 1, 5);
                var delaySeconds = Math.Min(1 << consecutiveErrors, 20);
                StatusChanged?.Invoke($"Voice engine retrying in {delaySeconds}s: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
            }
            finally
            {
                AudioCaptureService.TryDelete(file);
            }
        }
    }

    private async Task HandleTranscriptAsync(
        string text,
        AppSettings settings,
        bool bypassWakeWord)
    {
        text = Cleanup(text);
        if (string.IsNullOrWhiteSpace(text))
            return;

        TranscriptReceived?.Invoke(text);

        var commandText = text;
        var awake = bypassWakeWord || !settings.RequireWakeWord ||
                    DateTime.UtcNow < _conversationUntilUtc;

        if (!bypassWakeWord && settings.RequireWakeWord)
        {
            var wake = FindWakeWord(commandText, settings.WakeWords);
            if (wake is not null)
            {
                awake = true;
                _conversationUntilUtc =
                    DateTime.UtcNow.AddSeconds(Math.Max(8, settings.ConversationSeconds));

                commandText = RemoveWakeWord(commandText, wake).Trim(' ', ',', '.', '!', '?', '।');

                if (string.IsNullOrWhiteSpace(commandText))
                {
                    _speech.Speak("Yes?");
                    StatusChanged?.Invoke("Wake word detected");
                    return;
                }
            }
        }

        if (!awake)
            return;

        if (ContainsAny(commandText, "go to sleep", "stop conversation", "ঘুমাও", "চুপ থাকো"))
        {
            _conversationUntilUtc = DateTime.MinValue;
            _speech.Speak("Okay.");
            return;
        }

        _conversationUntilUtc =
            DateTime.UtcNow.AddSeconds(Math.Max(8, settings.ConversationSeconds));

        var result = await _commands.ProcessAsync(commandText);
        _history.Add(commandText, result);

        CommandCompleted?.Invoke(result);
        _speech.Speak(result.SpokenResponse);
        StatusChanged?.Invoke(result.Success ? "Command complete" : "Command not matched");
    }

    private static string Cleanup(string value)
    {
        value = value.Replace("[BLANK_AUDIO]", "", StringComparison.OrdinalIgnoreCase);
        value = Regex.Replace(value, @"\s+", " ");
        return value.Trim();
    }

    private static string? FindWakeWord(string text, IEnumerable<string> wakeWords) =>
        wakeWords
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .OrderByDescending(w => w.Length)
            .FirstOrDefault(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string RemoveWakeWord(string text, string wakeWord)
    {
        var index = text.IndexOf(wakeWord, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? text
            : text.Remove(index, wakeWord.Length);
    }

    private static bool ContainsAny(string input, params string[] needles) =>
        needles.Any(n => input.Contains(n, StringComparison.OrdinalIgnoreCase));
}
