namespace NovaAgent.Models;

public sealed class PendingConfirmation
{
    public required string Description { get; init; }
    public required Func<Task<CommandResult>> ExecuteAsync { get; init; }
    public DateTime ExpiresUtc { get; init; } = DateTime.UtcNow.AddSeconds(30);
}
