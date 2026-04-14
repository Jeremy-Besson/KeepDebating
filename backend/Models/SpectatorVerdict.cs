namespace TryingStuff.Models;

public sealed class SpectatorVerdict
{
    public required string SpectatorName { get; init; }

    public required string Perspective { get; init; }

    public required string Winner { get; init; }

    public required int Confidence { get; init; }

    public required string Rationale { get; init; }
}
