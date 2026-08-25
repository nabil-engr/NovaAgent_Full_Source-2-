using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class WhisperService : IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };
    private readonly HttpClient _healthHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly SettingsService _settingsService;
    private Process? _server;
    private readonly SemaphoreSlim _startupGate = new(1, 1);

    public WhisperService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<(bool Ok, string Message)> EnsureServerAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (await IsReadyAsync(settings.WhisperPort, cancellationToken))
            return (true, "Whisper ready");

        await _startupGate.WaitAsync(cancellationToken);
        try
        {
            if (await IsReadyAsync(settings.WhisperPort, cancellationToken))
                return (true, "Whisper ready");

            var exe = _settingsService.ResolveAppPath(settings.WhisperServerPath);
            var model = _settingsService.ResolveAppPath(settings.WhisperModelPath);

            if (!File.Exists(exe))
                return (false, $"Missing whisper-server.exe: {exe}");

            if (!File.Exists(model))
                return (false, $"Missing Whisper model: {model}");

            if (_server is { HasExited: true })
            {
                AppLog.Error($"Local Whisper server exited with code {_server.ExitCode}.");
                _server.Dispose();
                _server = null;
            }

            if (_server is null)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    WorkingDirectory = Path.GetDirectoryName(exe)!
                };
                startInfo.ArgumentList.Add("-m");
                startInfo.ArgumentList.Add(model);
                startInfo.ArgumentList.Add("--host");
                startInfo.ArgumentList.Add("127.0.0.1");
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add(settings.WhisperPort.ToString());
                startInfo.ArgumentList.Add("-l");
                startInfo.ArgumentList.Add("auto");
                startInfo.ArgumentList.Add("-nth");
                startInfo.ArgumentList.Add("0.70");

                _server = Process.Start(startInfo);
                if (_server is null)
                    return (false, "Windows could not start the local Whisper server.");

                AppLog.Info($"Started local Whisper server on port {settings.WhisperPort}.");
            }

            for (var i = 0; i < 60; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsReadyAsync(settings.WhisperPort, cancellationToken))
                    return (true, "Whisper ready");

                await Task.Delay(500, cancellationToken);
            }

            return (false, "Whisper server did not become ready.");
        }
        finally
        {
            _startupGate.Release();
        }
    }

    public async Task<string> TranscribeAsync(
        string wavFile,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var ready = await EnsureServerAsync(settings, cancellationToken);
        if (!ready.Ok)
            throw new InvalidOperationException(ready.Message);

        using var form = new MultipartFormDataContent();
        await using var stream = File.OpenRead(wavFile);
        using var audio = new StreamContent(stream);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        form.Add(audio, "file", Path.GetFileName(wavFile));
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0.0"), "temperature");
        form.Add(new StringContent("0.2"), "temperature_inc");

        using var response = await _http.PostAsync(
            $"http://127.0.0.1:{settings.WhisperPort}/inference",
            form,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Whisper error: {body}");

        using var json = JsonDocument.Parse(body);

        if (json.RootElement.TryGetProperty("text", out var text))
            return text.GetString()?.Trim() ?? "";

        return "";
    }

    private async Task<bool> IsReadyAsync(int port, CancellationToken ct)
    {
        try
        {
            using var response = await _healthHttp.GetAsync($"http://127.0.0.1:{port}/", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _healthHttp.Dispose();
        try
        {
            if (_server is { HasExited: false })
                _server.Kill(entireProcessTree: true);
        }
        catch { }

        _server?.Dispose();
        _startupGate.Dispose();
    }
}
