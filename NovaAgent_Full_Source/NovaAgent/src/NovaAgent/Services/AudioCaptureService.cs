using NAudio.Wave;

namespace NovaAgent.Services;

public sealed class AudioCaptureService
{
    private readonly string _tempDir;

    public AudioCaptureService()
    {
        _tempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaAgent", "Temp");
        Directory.CreateDirectory(_tempDir);
    }

    public async Task<(string FilePath, float Peak)> CaptureAsync(
        int milliseconds,
        int deviceNumber,
        CancellationToken cancellationToken)
    {
        var file = Path.Combine(_tempDir, $"voice-{Guid.NewGuid():N}.wav");
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var input = new WaveIn
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100,
            NumberOfBuffers = 3
        };

        using var writer = new WaveFileWriter(file, input.WaveFormat);
        float peak = 0;

        input.DataAvailable += (_, e) =>
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            writer.Flush();

            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                var value = Math.Abs(sample / 32768f);
                if (value > peak) peak = value;
            }
        };

        input.RecordingStopped += (_, _) => stopped.TrySetResult(true);

        using var registration = cancellationToken.Register(() =>
        {
            try { input.StopRecording(); } catch { }
        });

        input.StartRecording();

        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (OperationCanceledException) { }

        try { input.StopRecording(); } catch { }
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

        return (file, peak);
    }

    public static IReadOnlyList<(int Number, string Name)> GetInputDevices()
    {
        var devices = new List<(int Number, string Name)>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i);
            devices.Add((i, capabilities.ProductName));
        }

        return devices;
    }

    public static void TryDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
