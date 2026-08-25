namespace NovaAgent.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool AlwaysListening { get; set; } = false;
    public bool RequireWakeWord { get; set; } = true;
    public bool SpeakResponses { get; set; } = true;
    public bool AutoStart { get; set; } = false;
    public bool CloseToTray { get; set; } = true;

    public int ChunkMilliseconds { get; set; } = 4200;
    public int ConversationSeconds { get; set; } = 22;
    public float MinimumPeak { get; set; } = 0.018f;
    public int WhisperPort { get; set; } = 8178;
    public int MicrophoneDeviceNumber { get; set; } = 0;
    public int SpeechRate { get; set; } = 1;
    public int SpeechVolume { get; set; } = 90;
    public int FileSearchLimit { get; set; } = 25000;

    public string[] WakeWords { get; set; } = ["nova", "নোভা", "নোভা,", "hey nova", "হেই নোভা"];

    public string WhisperServerPath { get; set; } = @"runtime\whisper\whisper-server.exe";
    public string WhisperModelPath { get; set; } = @"runtime\whisper\ggml-base.bin";

    public Dictionary<string, string> CustomApps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
