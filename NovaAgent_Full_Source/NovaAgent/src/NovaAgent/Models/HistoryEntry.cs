namespace NovaAgent.Models;

public sealed class HistoryEntry
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Command { get; set; } = "";
    public string Result { get; set; } = "";
    public bool Success { get; set; }
}
