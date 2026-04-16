namespace TryingStuff.Models;

public sealed class ModerationNote
{
    public required bool Flagged { get; init; }
    public required string[] Issues { get; init; }
    public required string Summary { get; init; }
}
