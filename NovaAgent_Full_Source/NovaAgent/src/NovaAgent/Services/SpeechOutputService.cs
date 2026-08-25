using System.Speech.Synthesis;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class SpeechOutputService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private int _speaking;

    public bool IsSpeaking => Volatile.Read(ref _speaking) > 0;
    public bool Enabled { get; set; } = true;

    public SpeechOutputService()
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.Rate = 1;
        _synth.Volume = 90;
        _synth.SpeakCompleted += (_, _) => Interlocked.Exchange(ref _speaking, 0);
    }

    public void Configure(AppSettings settings)
    {
        Enabled = settings.SpeakResponses;
        _synth.Rate = Math.Clamp(settings.SpeechRate, -10, 10);
        _synth.Volume = Math.Clamp(settings.SpeechVolume, 0, 100);
    }

    public void Speak(string text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            _synth.SpeakAsyncCancelAll();
            Interlocked.Exchange(ref _speaking, 1);
            _synth.SpeakAsync(text);
        }
        catch
        {
            Interlocked.Exchange(ref _speaking, 0);
        }
    }

    public void Dispose() => _synth.Dispose();
}
