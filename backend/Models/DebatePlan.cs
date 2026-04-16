namespace TryingStuff.Models;

public sealed class RoundPlan
{
    public required int Round { get; init; }
    public required string ArgumentType { get; init; }
}

public sealed class StancePlan
{
    public required string Stance { get; init; }
    public required IReadOnlyList<RoundPlan> Rounds { get; init; }

    public string? GetArgumentType(int round) =>
        Rounds.FirstOrDefault(r => r.Round == round)?.ArgumentType;
}

public sealed class DebatePlan
{
    public required StancePlan Pro { get; init; }
    public required StancePlan Con { get; init; }

    public string? GetArgumentType(string stance, int round) =>
        string.Equals(stance, "PRO", StringComparison.OrdinalIgnoreCase)
            ? Pro.GetArgumentType(round)
            : Con.GetArgumentType(round);
}
