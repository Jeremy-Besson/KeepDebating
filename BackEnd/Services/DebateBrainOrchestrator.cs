using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using TryingStuff.Models;

namespace TryingStuff.Services;

public sealed class DebateBrainOrchestrator
{
    private static readonly Action<ILogger, string, string, Exception?> LogInitializingBrain =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1000, nameof(LogInitializingBrain)),
            "Initializing debate brain. AzureOpenAI endpoint: {Endpoint}, deployment: {Deployment}");

    private static readonly Action<ILogger, string, Exception?> LogUnparsableDecisionPayload =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1001, nameof(LogUnparsableDecisionPayload)),
            "Brain returned an unparsable decision payload. Raw (truncated): {Payload}");

    private static readonly Action<ILogger, string, Exception?> LogBrainDecisionFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(LogBrainDecisionFailed)),
            "Brain decision failed. ExceptionType: {ExceptionType}. Falling back to strict alternation.");

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<DebateBrainOrchestrator> _logger;

    public DebateBrainOrchestrator(
        string endpoint,
        string apiKey,
        string model,
        Dictionary<string, string> wikipediaCache,
        ILogger<DebateBrainOrchestrator> logger,
        ILogger<WikipediaPlugin> wikipediaLogger)
    {
        _logger = logger;

        var connectorConfig = BuildAzureOpenAiConnectorConfig(endpoint, model);
        LogInitializingBrain(_logger, connectorConfig.ResourceEndpoint, connectorConfig.DeploymentName, null);

        _kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                connectorConfig.DeploymentName,
                connectorConfig.ResourceEndpoint,
                apiKey)
            .Build();

        _kernel.Plugins.AddFromObject(new WikipediaPlugin(wikipediaCache, wikipediaLogger), "Wikipedia");
        _chatCompletion = _kernel.Services.GetRequiredService<IChatCompletionService>();
    }

    public async Task<BrainDecision> DecideAsync(BrainDecisionContext context, CancellationToken cancellationToken = default)
    {
        if (!context.HasAskedHumanFeedback
            && string.IsNullOrWhiteSpace(context.LastHumanAnswer)
            && context.MainTurnsCompleted >= 2)
        {
            return new BrainDecision
            {
                Decision = "ask-user",
                SpeakerStance = null,
                TurnKind = null,
                Reason = "Collecting one human preference to steer the remainder of the debate.",
                Question = "Before we continue, what should the debaters prioritize most: child wellbeing, practical routines, long-term habits, or evidence quality?",
                RetrievedFacts = []
            };
        }

        var stateJson = JsonSerializer.Serialize(context);
        var prompt = $$"""
        You are the orchestration brain for a debate system.

        Policy constraints:
        - Default behavior is strict alternation between PRO and CON.
        - You may request one follow-up turn in the same round only when the latest turn needs clarification.
        - You may ask a human question only when the debate lacks critical context to continue responsibly.
        - Keep human questions concise and actionable.
                - You have a Wikipedia tool. Use it when evidence is thin or stale and choose precise search queries.

        Return strict JSON only with this shape:
        {
          "decision": "next-turn" | "ask-user",
          "speakerStance": "PRO" | "CON" | null,
          "turnKind": "argument" | "follow-up" | null,
          "reason": "short reason",
                    "question": "question text or null",
                    "retrievedFacts": ["concise cited fact strings for next speaker"]
        }

        Debate state:
        {{stateJson}}
        """;

        try
        {
            var settings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.15,
                TopP = 0.8,
                MaxTokens = 420,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(prompt);

            var reply = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel,
                cancellationToken);

            var raw = reply.Content ?? string.Empty;

            if (!TryParseDecisionRaw(raw, out var parsed))
            {
                LogUnparsableDecisionPayload(_logger, Truncate(raw, 700), null);
                return StrictAlternationFallback(context, "Fallback because the brain returned invalid JSON.");
            }

            return NormalizeDecision(parsed, context);
        }
        catch (Exception ex)
        {
            LogBrainDecisionFailed(_logger, ex.GetType().Name, ex);
            return StrictAlternationFallback(context, "Fallback after orchestration failure. See backend logs.");
        }
    }

    private static bool TryParseDecisionRaw(string raw, out BrainDecisionRaw? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        foreach (var candidate in BuildParseCandidates(raw))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<BrainDecisionRaw>(candidate, options);
                if (parsed is not null)
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // Try the next candidate representation.
            }
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
        {
            yield return trimmed[firstBrace..(lastBrace + 1)];
        }

        var cleanedFence = trimmed
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (!string.Equals(cleanedFence, trimmed, StringComparison.Ordinal))
        {
            yield return cleanedFence;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "...");
    }

    private static AzureOpenAiConnectorConfig BuildAzureOpenAiConnectorConfig(string endpoint, string fallbackDeploymentName)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return new AzureOpenAiConnectorConfig(endpoint.TrimEnd('/'), fallbackDeploymentName);
        }

        var pathSegments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        var deploymentIndex = Array.FindIndex(pathSegments,
            segment => segment.Equals("deployments", StringComparison.OrdinalIgnoreCase));

        if (deploymentIndex >= 0 && deploymentIndex + 1 < pathSegments.Length)
        {
            var deploymentFromPath = pathSegments[deploymentIndex + 1];
            var resourceEndpoint = $"{uri.Scheme}://{uri.Authority}";
            return new AzureOpenAiConnectorConfig(resourceEndpoint, deploymentFromPath);
        }

        return new AzureOpenAiConnectorConfig($"{uri.Scheme}://{uri.Authority}", fallbackDeploymentName);
    }

    private static BrainDecision NormalizeDecision(BrainDecisionRaw? raw, BrainDecisionContext context)
    {
        if (raw is null)
        {
            return StrictAlternationFallback(context, "Fallback because the brain returned no decision.");
        }

        var decision = raw.Decision?.Trim().ToLowerInvariant();
        if (decision == "ask-user")
        {
            var safeQuestion = string.IsNullOrWhiteSpace(raw.Question)
                ? "Could you clarify what priority should guide the next argument?"
                : raw.Question.Trim();

            return new BrainDecision
            {
                Decision = "ask-user",
                SpeakerStance = null,
                TurnKind = null,
                Reason = NonEmptyReason(raw.Reason, "Human input is required to proceed."),
                Question = safeQuestion,
                RetrievedFacts = NormalizeRetrievedFacts(raw.RetrievedFacts)
            };
        }

        var expected = context.ExpectedStance;
        var requestedStance = NormalizeStance(raw.SpeakerStance, expected);
        var turnKind = NormalizeTurnKind(raw.TurnKind, "argument");

        var followUpAllowed = context.CanUseFollowUp && context.LastTurnStance is not null;
        if (turnKind == "follow-up" && !followUpAllowed)
        {
            return StrictAlternationFallback(context, "Follow-up was requested but is not allowed in this state.");
        }

        if (turnKind == "follow-up" && context.LastTurnStance is not null)
        {
            requestedStance = context.LastTurnStance;
        }

        if (turnKind == "argument")
        {
            requestedStance = expected;
        }

        return new BrainDecision
        {
            Decision = "next-turn",
            SpeakerStance = requestedStance,
            TurnKind = turnKind,
            Reason = NonEmptyReason(raw.Reason, "Proceeding with next turn."),
            Question = null,
            RetrievedFacts = NormalizeRetrievedFacts(raw.RetrievedFacts)
        };
    }

    private static BrainDecision StrictAlternationFallback(BrainDecisionContext context, string reason)
    {
        return new BrainDecision
        {
            Decision = "next-turn",
            SpeakerStance = context.ExpectedStance,
            TurnKind = "argument",
            Reason = reason,
            Question = null,
            RetrievedFacts = []
        };
    }

    private static string[] NormalizeRetrievedFacts(IReadOnlyList<string>? facts)
    {
        if (facts is null || facts.Count == 0)
        {
            return [];
        }

        return facts
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Select(static f => f.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static string NormalizeStance(string? stance, string fallback)
    {
        var value = stance?.Trim().ToUpperInvariant();
        return value is "PRO" or "CON" ? value : fallback;
    }

    private static string NormalizeTurnKind(string? turnKind, string fallback)
    {
        var value = turnKind?.Trim().ToLowerInvariant();
        return value is "argument" or "follow-up" ? value : fallback;
    }

    private static string NonEmptyReason(string? reason, string fallback)
    {
        return string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
    }

    private sealed class BrainDecisionRaw
    {
        public string? Decision { get; init; }
        public string? SpeakerStance { get; init; }
        public string? TurnKind { get; init; }
        public string? Reason { get; init; }
        public string? Question { get; init; }
        public IReadOnlyList<string>? RetrievedFacts { get; init; }
    }

    private sealed record AzureOpenAiConnectorConfig(string ResourceEndpoint, string DeploymentName);
}

public sealed class BrainDecisionContext
{
    public required string Topic { get; init; }
    public required int Round { get; init; }
    public required int MainTurnsCompleted { get; init; }
    public required int MaxMainTurns { get; init; }
    public required string ExpectedStance { get; init; }
    public required bool CanUseFollowUp { get; init; }
    public required bool HasAskedHumanFeedback { get; init; }
    public string? LastTurnStance { get; init; }
    public string? LastTurnMessage { get; init; }
    public required IReadOnlyList<DebateTurn> RecentTurns { get; init; }
    public string? LastHumanAnswer { get; init; }
}

public sealed class BrainDecision
{
    public required string Decision { get; init; }
    public string? SpeakerStance { get; init; }
    public string? TurnKind { get; init; }
    public required string Reason { get; init; }
    public string? Question { get; init; }
    public required IReadOnlyList<string> RetrievedFacts { get; init; }
}
