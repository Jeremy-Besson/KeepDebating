namespace TryingStuff.Models;

public sealed class HumanLoopCheckpoint
{
    public required string CheckpointId { get; init; }

    public required string Reason { get; init; }

    public required string Question { get; init; }

    public string? Answer { get; set; }

    public required string Status { get; set; }

    public required DateTimeOffset AskedAt { get; init; }

    public DateTimeOffset? AnsweredAt { get; set; }
}
