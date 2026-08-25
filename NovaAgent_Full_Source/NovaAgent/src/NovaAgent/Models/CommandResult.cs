namespace NovaAgent.Models;

public sealed record CommandResult(
    bool Success,
    string SpokenResponse,
    string Detail = "",
    bool NeedsConfirmation = false
);
