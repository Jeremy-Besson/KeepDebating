namespace TryingStuff.Models;

public sealed class DebateTurn
{
    public required int Round { get; init; }

    public required string Speaker { get; init; }

    public required string Stance { get; init; }

    public required string Message { get; init; }

    public required string TurnKind { get; init; }

    public string? OrchestratorReason { get; init; }

    public string? ReflectionText { get; init; }

    public required IReadOnlyList<string> ToolFactsUsed { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
