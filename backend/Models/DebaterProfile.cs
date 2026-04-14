namespace TryingStuff.Models;

public sealed class DebaterProfile
{
    public required string Name { get; init; }

    public required string Stance { get; init; }

    public required string Character { get; init; }

    public required string SystemPrompt { get; init; }
}
