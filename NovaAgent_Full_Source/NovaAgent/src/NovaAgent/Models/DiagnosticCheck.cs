namespace NovaAgent.Models;

public sealed record DiagnosticCheck(
    string Name,
    bool Passed,
    string Message,
    string Suggestion = "")
{
    public string Status => Passed ? "PASS" : "ACTION NEEDED";
}
