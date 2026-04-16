using Azure;
using Azure.AI.Inference;
using TryingStuff.Models;

namespace TryingStuff.Services;

public sealed class DebateSpectatorPanel
{
    private readonly ChatCompletionsClient _client;
    private readonly string _model;
    private static readonly IReadOnlyList<SpectatorProfile> SpectatorCatalog =
    [
        new("Maya",      "Parent focused on healthy routines and stress levels",                                                    0.135, 0.18,  0.135, 0.315, 0.135, 0.1),
        new("Noah",      "Teacher focused on learning outcomes and attention",                                                      0.18,  0.18,  0.18,  0.135, 0.225, 0.1),
        new("Dr. Lin",   "Child development specialist focused on emotional impact",                                                0.135, 0.18,  0.135, 0.36,  0.09,  0.1),
        new("Evelyn",    "Retired 78-year-old grandmother focused on long-term character building",                                 0.135, 0.18,  0.27,  0.18,  0.135, 0.1),
        new("Zoe",       "16-year-old student focused on peer habits and modern teen attention patterns",                           0.18,  0.18,  0.135, 0.18,  0.225, 0.1),
        new("Mr. Grant", "Conservative father focused on discipline, accountability, and traditional family structure",             0.18,  0.135, 0.27,  0.135, 0.18,  0.1),
        new("Amira",     "Progressive social worker focused on inclusion, emotional safety, and equity",                           0.135, 0.135, 0.135, 0.36,  0.135, 0.1),
        new("Coach Ben", "Youth sports coach focused on self-control, routine, and delayed gratification",                         0.135, 0.225, 0.225, 0.135, 0.18,  0.1),
        new("Rui",       "Software engineer focused on notification design and compulsive digital behavior",                       0.225, 0.135, 0.135, 0.27,  0.135, 0.1),
        new("Farah",     "School counselor focused on anxiety triggers and self-esteem",                                           0.135, 0.135, 0.135, 0.36,  0.135, 0.1),
        new("Paolo",     "Pediatric nurse focused on sleep hygiene and stress responses in children",                              0.135, 0.18,  0.135, 0.315, 0.135, 0.1),
        new("Kim",       "Single parent working two jobs focused on practical supervision constraints",                            0.135, 0.135, 0.18,  0.18,  0.27,  0.1),
        new("Ari",       "College freshman who grew up with virtual pets and reflects on personal outcomes",                       0.18,  0.18,  0.18,  0.18,  0.18,  0.1),
        new("Sofia",     "Montessori educator focused on autonomy, routines, and intrinsic motivation",                            0.135, 0.135, 0.27,  0.18,  0.18,  0.1)
    ];

    public DebateSpectatorPanel(ChatCompletionsClient client, string model)
    {
        _client = client;
        _model = model;
    }

    public IReadOnlyList<SpectatorVerdict> Evaluate(
        DebateTranscript transcript,
        int spectatorCount,
        Action<SpectatorVerdict>? onVerdictGenerated = null)
    {
        var selectedSpectators = SelectRandomSpectators(spectatorCount);

        var results = new List<SpectatorVerdict>();
        var transcriptText = BuildTranscriptText(transcript);
        var moderationHistoryText = BuildModerationHistoryText(transcript);

        foreach (var spectator in selectedSpectators)
        {
            var verdict = EvaluateSingleSpectator(spectator, transcript.Topic, transcriptText, moderationHistoryText);
            results.Add(verdict);
            onVerdictGenerated?.Invoke(verdict);
        }

        return results;
    }

    public async Task<IReadOnlyList<SpectatorVerdict>> EvaluateAsync(
        DebateTranscript transcript,
        int spectatorCount,
        Func<SpectatorVerdict, Task>? onVerdictGenerated = null)
    {
        var selectedSpectators = SelectRandomSpectators(spectatorCount);

        var results = new List<SpectatorVerdict>();
        var transcriptText = BuildTranscriptText(transcript);
        var moderationHistoryText = BuildModerationHistoryText(transcript);

        foreach (var spectator in selectedSpectators)
        {
            var verdict = EvaluateSingleSpectator(spectator, transcript.Topic, transcriptText, moderationHistoryText);
            results.Add(verdict);
            if (onVerdictGenerated is not null)
            {
                await onVerdictGenerated(verdict);
            }
        }

        return results;
    }

    private static List<SpectatorProfile> SelectRandomSpectators(int requestedCount)
    {
        var count = Math.Clamp(requestedCount, 1, SpectatorCatalog.Count);

        return SpectatorCatalog
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();
    }

    private SpectatorVerdict EvaluateSingleSpectator(
        SpectatorProfile spectator,
        string topic,
        string transcriptText,
        string moderationHistoryText)
    {
        var systemPrompt = $"""
            You are {spectator.Name}: {spectator.Perspective}.
            You are watching a debate and judging it entirely through the lens of your own background and priorities.
            What matters most to you shapes how you score — arguments that align with your concerns should score higher.
            Return ONLY the required key-value lines. Each score must be an integer from 0 to 10.
            """;

        var options = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(BuildEvaluationPrompt(spectator, topic, transcriptText, moderationHistoryText))
            },
            Temperature = 0.9f,
            NucleusSamplingFactor = 0.95f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.1f,
            Model = _model
        };

        try
        {
            Response<ChatCompletions> response = _client.Complete(options);
            var raw = response.Value.Content ?? string.Empty;
            return ParseVerdict(spectator, raw);
        }
        catch (Exception ex)
        {
            return new SpectatorVerdict
            {
                SpectatorName = spectator.Name,
                Perspective = spectator.Perspective,
                Winner = "TIE",
                Confidence = 0,
                Rationale = $"Fallback verdict due to evaluation error: {ex.Message}"
            };
        }
    }

    private static string BuildEvaluationPrompt(
        SpectatorProfile spectator,
        string topic,
        string transcriptText,
        string moderationHistoryText)
    {
        return $"""
        Debate topic: {topic}

        Your identity: {spectator.Name} — {spectator.Perspective}.
        Judge this debate strictly from your own point of view. Arguments that speak directly to your background
        and concerns should score higher. Arguments that ignore what you care about should score lower.

        Moderator flags raised during this debate:
        {moderationHistoryText}

        Full transcript:
        {transcriptText}

        Task:
        Score PRO and CON independently on these 6 criteria (0-10 integer each).
        Let your perspective drive the numbers — do not try to be neutral.
          - LOGIC: how solid and convincing the reasoning felt to you personally.
          - REBUTTAL: how effectively each side responded to the opponent's points.
          - CONSISTENCY: how well each side stayed on its position.
          - WELLBEING: how well each side addressed the impact on children from your angle.
          - PRACTICALITY: how realistic each side's suggestions felt given what you know.
          - ACCURACY: how well each side avoided logical fallacies, stayed on topic, and grounded claims in evidence.

        Return exactly these 13 lines and nothing else:
          PRO_LOGIC: <0-10>
          PRO_REBUTTAL: <0-10>
          PRO_CONSISTENCY: <0-10>
          PRO_WELLBEING: <0-10>
          PRO_PRACTICALITY: <0-10>
          PRO_ACCURACY: <0-10>
          CON_LOGIC: <0-10>
          CON_REBUTTAL: <0-10>
          CON_CONSISTENCY: <0-10>
          CON_WELLBEING: <0-10>
          CON_PRACTICALITY: <0-10>
          CON_ACCURACY: <0-10>
          RATIONALE: <one paragraph written in your voice, reflecting your background and concerns>
        """;
    }

    private static SpectatorVerdict ParseVerdict(SpectatorProfile spectator, string raw)
    {
        var scores = ParseScoreMap(raw);

        var proWeighted =
            ReadScore(scores, "PRO_LOGIC")        * spectator.LogicWeight +
            ReadScore(scores, "PRO_REBUTTAL")     * spectator.RebuttalWeight +
            ReadScore(scores, "PRO_CONSISTENCY")  * spectator.ConsistencyWeight +
            ReadScore(scores, "PRO_WELLBEING")    * spectator.WellbeingWeight +
            ReadScore(scores, "PRO_PRACTICALITY") * spectator.PracticalityWeight +
            ReadScore(scores, "PRO_ACCURACY")     * spectator.AccuracyWeight;

        var conWeighted =
            ReadScore(scores, "CON_LOGIC")        * spectator.LogicWeight +
            ReadScore(scores, "CON_REBUTTAL")     * spectator.RebuttalWeight +
            ReadScore(scores, "CON_CONSISTENCY")  * spectator.ConsistencyWeight +
            ReadScore(scores, "CON_WELLBEING")    * spectator.WellbeingWeight +
            ReadScore(scores, "CON_PRACTICALITY") * spectator.PracticalityWeight +
            ReadScore(scores, "CON_ACCURACY")     * spectator.AccuracyWeight;

        var difference = proWeighted - conWeighted;
        var absDifference = Math.Abs(difference);

        var winner = absDifference < 0.6
            ? "TIE"
            : (difference > 0 ? "PRO" : "CON");

        var confidence = winner == "TIE"
            ? Math.Clamp((int)Math.Round(55 + absDifference * 3), 50, 80)
            : Math.Clamp((int)Math.Round(58 + absDifference * 5), 55, 98);

        var rationale = ReadTextValue(scores, "RATIONALE", "No rationale provided by spectator model.");

        return new SpectatorVerdict
        {
            SpectatorName = spectator.Name,
            Perspective = spectator.Perspective,
            Winner = winner,
            Confidence = confidence,
            Rationale = rationale
        };
    }

    private static Dictionary<string, string> ParseScoreMap(string raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = raw
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim());

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        return map;
    }

    private static int ReadScore(Dictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var rawValue))
        {
            return 5;
        }

        return int.TryParse(rawValue, out var score)
            ? Math.Clamp(score, 0, 10)
            : 5;
    }

    private static string ReadTextValue(Dictionary<string, string> map, string key, string fallback)
    {
        if (!map.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(rawValue) ? fallback : rawValue;
    }

    private static string BuildTranscriptText(DebateTranscript transcript)
    {
        var lines = transcript.Turns
            .Select(t => $"Round {t.Round} {t.Stance}: {t.Message}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildModerationHistoryText(DebateTranscript transcript)
    {
        var flagged = transcript.Turns
            .Where(t => t.Moderation?.Flagged == true)
            .Select(t => $"Round {t.Round} {t.Stance}: {string.Join(", ", t.Moderation!.Issues)}")
            .ToList();

        return flagged.Count == 0
            ? "No moderation flags were raised."
            : string.Join(Environment.NewLine, flagged);
    }

    private sealed record SpectatorProfile(
        string Name,
        string Perspective,
        double LogicWeight,
        double RebuttalWeight,
        double ConsistencyWeight,
        double WellbeingWeight,
        double PracticalityWeight,
        double AccuracyWeight);
}
