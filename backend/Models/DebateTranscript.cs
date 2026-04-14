namespace TryingStuff.Models;

public sealed class DebateTranscript
{
    public required string Topic { get; init; }

    public required string Model { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; set; }

    public required int Rounds { get; init; }

    public required List<DebateTurn> Turns { get; init; }

    public required List<HumanLoopCheckpoint> HumanLoopCheckpoints { get; init; }

    public required List<SpectatorVerdict> SpectatorVerdicts { get; init; }
}
