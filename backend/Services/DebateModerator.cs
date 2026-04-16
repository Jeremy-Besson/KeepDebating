using System.Text.Json;
using Azure.AI.Inference;
using Microsoft.Extensions.Logging;
using TryingStuff.Models;

namespace TryingStuff.Services;

public sealed class DebateModerator
{
    private static readonly Action<ILogger, int, string, bool, string, Exception?> LogModerationComplete =
        LoggerMessage.Define<int, string, bool, string>(
            LogLevel.Information,
            new EventId(4000, nameof(LogModerationComplete)),
            "Moderation complete. Round: {Round}, Stance: {Stance}, Flagged: {Flagged}, Issues: {Issues}");

    private static readonly Action<ILogger, int, string, string, Exception?> LogModerationFailed =
        LoggerMessage.Define<int, string, string>(
            LogLevel.Warning,
            new EventId(4001, nameof(LogModerationFailed)),
            "Moderation failed. Round: {Round}, Stance: {Stance}, Reason: {Reason}");

    private static readonly ModerationNote FallbackNote = new()
    {
        Flagged = false,
        Issues = [],
        Summary = "Moderation unavailable."
    };

    private readonly ChatCompletionsClient _client;
    private readonly string _model;
    private readonly ILogger<DebateModerator> _logger;

    public DebateModerator(ChatCompletionsClient client, string model, ILogger<DebateModerator> logger)
    {
        _client = client;
        _model = model;
        _logger = logger;
    }

    public async Task<ModerationNote> EvaluateAsync(
        DebateTurn turn,
        string topic,
        CancellationToken cancellationToken = default)
    {
        var factsBlock = turn.ToolFactsUsed.Count > 0
            ? string.Join("\n", turn.ToolFactsUsed.Select(f => $"- {f}"))
            : "No facts were provided to this debater.";

        var prompt = $$"""
            Debate topic: "{{topic}}"
            Stance: {{turn.Stance}}
            Round: {{turn.Round}}

            Facts the debater was given:
            {{factsBlock}}

            Debater's argument:
            "{{turn.Message}}"

            Evaluate this argument for the following issues only:
            1. Logical fallacies (ad hominem, straw man, false dichotomy, slippery slope, appeal to emotion, etc.)
            2. Off-topic content (argument does not address the debate topic)
            3. Unsupported factual claims (statistics or facts not grounded in the provided facts above)

            Be strict but fair. Minor rhetorical flourishes are not fallacies.
            Set "flagged" to true only if at least one clear issue is present.

            Return strict JSON only:
            {
              "flagged": true,
              "issues": ["unsupported claim", "ad hominem"],
              "summary": "One sentence describing what was found, or confirming the argument was clean."
            }
            """;

        try
        {
            var options = new ChatCompletionsOptions
            {
                Messages =
                {
                    new ChatRequestSystemMessage(
                        "You are a neutral debate moderator. Your only role is to identify clear logical fallacies, " +
                        "off-topic arguments, and unsupported factual claims. You have no stance on the topic."),
                    new ChatRequestUserMessage(prompt)
                },
                Temperature = 0.2f,
                NucleusSamplingFactor = 0.7f,
                MaxTokens = 150,
                Model = _model
            };

            var response = await _client.CompleteAsync(options, cancellationToken);
            var raw = response.Value.Content ?? string.Empty;

            if (!TryParseModerationRaw(raw, out var parsed) || parsed is null)
            {
                LogModerationFailed(_logger, turn.Round, turn.Stance, "Unparsable JSON response", null);
                return FallbackNote;
            }

            var issues = parsed.Issues?
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .ToArray() ?? [];

            var note = new ModerationNote
            {
                Flagged = parsed.Flagged && issues.Length > 0,
                Issues = issues,
                Summary = string.IsNullOrWhiteSpace(parsed.Summary)
                    ? (parsed.Flagged ? "Issues were identified." : "Argument passed moderation.")
                    : parsed.Summary.Trim()
            };

            LogModerationComplete(
                _logger,
                turn.Round,
                turn.Stance,
                note.Flagged,
                note.Issues.Length > 0 ? string.Join(", ", note.Issues) : "none",
                null);

            return note;
        }
        catch (Exception ex)
        {
            LogModerationFailed(_logger, turn.Round, turn.Stance, ex.GetType().Name, ex);
            return FallbackNote;
        }
    }

    private static bool TryParseModerationRaw(string raw, out ModerationResultRaw? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var candidate in BuildParseCandidates(raw))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<ModerationResultRaw>(candidate, options);
                if (parsed is not null) return true;
            }
            catch (JsonException) { }
        }

        return false;
    }

    private static IEnumerable<string> BuildParseCandidates(string raw)
    {
        var trimmed = raw.Trim();
        yield return trimmed;

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            yield return trimmed[firstBrace..(lastBrace + 1)];

        var cleanedFence = trimmed
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (!string.Equals(cleanedFence, trimmed, StringComparison.Ordinal))
            yield return cleanedFence;
    }

    private sealed class ModerationResultRaw
    {
        public bool Flagged { get; init; }
        public IReadOnlyList<string>? Issues { get; init; }
        public string? Summary { get; init; }
    }
}
