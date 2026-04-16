# Plan: Multi-Agent Moderator

**Phase B — Technical Blueprint**

---

## Status
- [x] Phase A: Analysis
- [x] Phase B: Planning
- [ ] Phase C: Implementation, Testing & PR

---

## Scope

Six files change. Two new files created.

| File | Change type |
|---|---|
| `backend/Models/ModerationNote.cs` | New file |
| `backend/Models/DebateTurn.cs` | Add one mutable field |
| `backend/Services/DebateModerator.cs` | New file |
| `backend/Services/DebateOrchestrator.cs` | Inject moderator, run after main turns, add callback param |
| `backend/Services/DebateSpectatorPanel.cs` | 6th criterion, rebalanced weights, moderation history in prompt |
| `backend/Program.cs` | Construct moderator, wire callback, emit `moderation` SSE event |

---

## File 1 — `backend/Models/ModerationNote.cs` (new file)

```csharp
namespace TryingStuff.Models;

public sealed class ModerationNote
{
    public required bool Flagged { get; init; }
    public required string[] Issues { get; init; }
    public required string Summary { get; init; }
}
```

---

## File 2 — `backend/Models/DebateTurn.cs`

Add one nullable mutable field at the end of the class (after `Timestamp`):

```csharp
public required DateTimeOffset Timestamp { get; init; }
public ModerationNote? Moderation { get; set; }   // ← new
```

`set` (not `init`) because the moderator runs after the turn is constructed and already added to the transcript.

---

## File 3 — `backend/Services/DebateModerator.cs` (new file)

```csharp
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
```

**Notes:**
- `CompleteAsync` (async) instead of synchronous `Complete` — matches the existing async context
- `Temperature = 0.2f` for deterministic evaluation
- `MaxTokens = 150` — JSON is small
- No `FunctionChoiceBehavior` — plain completion, no tools
- `FallbackNote` is a static readonly to avoid allocating a new object on every failure

---

## File 4 — `backend/Services/DebateOrchestrator.cs`

### 4-A. New log delegate (after `LogReflectionGenerated`, before `_client` field)

```csharp
private static readonly Action<ILogger, int, string, bool, Exception?> LogModerationAttached =
    LoggerMessage.Define<int, string, bool>(
        LogLevel.Debug,
        new EventId(4002, nameof(LogModerationAttached)),
        "Moderation attached to turn. Round: {Round}, Stance: {Stance}, Flagged: {Flagged}");
```

### 4-B. Add `_moderator` field and update constructor

Add field after `_logger`:
```csharp
private readonly DebateModerator _moderator;
```

Add `DebateModerator moderator` parameter to constructor (after `logger`), before optional params:
```csharp
public DebateOrchestrator(
    ChatCompletionsClient client,
    DebateBrainOrchestrator brain,
    DebateKnowledgeStore knowledgeStore,
    string model,
    ILogger<DebateOrchestrator> logger,
    DebateModerator moderator,            // ← new
    string? debateStyle = null,
    string? proTone = null,
    string? conTone = null,
    int minTurnDelaySeconds = 0)
```

Assign in body: `_moderator = moderator;`

### 4-C. Update `ContinueDebateAsync` signature

Add `onModerationComplete` parameter before `cancellationToken`:

```csharp
public async Task<DebateProgressResult> ContinueDebateAsync(
    DebateSessionState session,
    bool waitForHumanInput,
    Func<DebateTurn, Task>? onTurnGenerated = null,
    Func<HumanLoopCheckpoint, Task>? onQuestionRaised = null,
    Func<DebateTurn, Task>? onModerationComplete = null,  // ← new
    CancellationToken cancellationToken = default)
```

### 4-D. Add moderation block after the `onTurnGenerated` call

Current code (in the while loop, near the end of the per-turn block):
```csharp
if (onTurnGenerated is not null)
{
    ...
    await onTurnGenerated(turn);
}
```

Add immediately after:
```csharp
if (string.Equals(turnKind, "argument", StringComparison.OrdinalIgnoreCase))
{
    var note = await _moderator.EvaluateAsync(turn, transcript.Topic, cancellationToken);
    turn.Moderation = note;
    LogModerationAttached(_logger, turn.Round, turn.Stance, note.Flagged, null);

    if (onModerationComplete is not null)
    {
        await onModerationComplete(turn);
    }
}
```

---

## File 5 — `backend/Services/DebateSpectatorPanel.cs`

### 5-A. Update `SpectatorProfile` record

Add `AccuracyWeight` as the 6th parameter:

```csharp
private sealed record SpectatorProfile(
    string Name,
    string Perspective,
    double LogicWeight,
    double RebuttalWeight,
    double ConsistencyWeight,
    double WellbeingWeight,
    double PracticalityWeight,
    double AccuracyWeight);   // ← new
```

### 5-B. Rebalance `SpectatorCatalog` — all 14 entries

Each existing weight × 0.9, new `AccuracyWeight = 0.1`. All rows sum to 1.0.

```csharp
private static readonly IReadOnlyList<SpectatorProfile> SpectatorCatalog =
[
    new("Maya",      "Parent focused on healthy routines and stress levels",                                                      0.135, 0.18,  0.135, 0.315, 0.135, 0.1),
    new("Noah",      "Teacher focused on learning outcomes and attention",                                                        0.18,  0.18,  0.18,  0.135, 0.225, 0.1),
    new("Dr. Lin",   "Child development specialist focused on emotional impact",                                                  0.135, 0.18,  0.135, 0.36,  0.09,  0.1),
    new("Evelyn",    "Retired 78-year-old grandmother focused on long-term character building",                                   0.135, 0.18,  0.27,  0.18,  0.135, 0.1),
    new("Zoe",       "16-year-old student focused on peer habits and modern teen attention patterns",                             0.18,  0.18,  0.135, 0.18,  0.225, 0.1),
    new("Mr. Grant", "Conservative father focused on discipline, accountability, and traditional family structure",               0.18,  0.135, 0.27,  0.135, 0.18,  0.1),
    new("Amira",     "Progressive social worker focused on inclusion, emotional safety, and equity",                             0.135, 0.135, 0.135, 0.36,  0.135, 0.1),
    new("Coach Ben", "Youth sports coach focused on self-control, routine, and delayed gratification",                           0.135, 0.225, 0.225, 0.135, 0.18,  0.1),
    new("Rui",       "Software engineer focused on notification design and compulsive digital behavior",                         0.225, 0.135, 0.135, 0.27,  0.135, 0.1),
    new("Farah",     "School counselor focused on anxiety triggers and self-esteem",                                             0.135, 0.135, 0.135, 0.36,  0.135, 0.1),
    new("Paolo",     "Pediatric nurse focused on sleep hygiene and stress responses in children",                                0.135, 0.18,  0.135, 0.315, 0.135, 0.1),
    new("Kim",       "Single parent working two jobs focused on practical supervision constraints",                              0.135, 0.135, 0.18,  0.18,  0.27,  0.1),
    new("Ari",       "College freshman who grew up with virtual pets and reflects on personal outcomes",                         0.18,  0.18,  0.18,  0.18,  0.18,  0.1),
    new("Sofia",     "Montessori educator focused on autonomy, routines, and intrinsic motivation",                              0.135, 0.135, 0.27,  0.18,  0.18,  0.1)
];
```

### 5-C. Update `Evaluate` and `EvaluateAsync`

Build `moderationHistoryText` once, pass alongside `transcriptText`:

```csharp
var transcriptText = BuildTranscriptText(transcript);
var moderationHistoryText = BuildModerationHistoryText(transcript);
// ... in loop:
var verdict = EvaluateSingleSpectator(spectator, transcript.Topic, transcriptText, moderationHistoryText);
```

### 5-D. Update `EvaluateSingleSpectator` signature

```csharp
private SpectatorVerdict EvaluateSingleSpectator(
    SpectatorProfile spectator,
    string topic,
    string transcriptText,
    string moderationHistoryText)
```

Pass `moderationHistoryText` to `BuildEvaluationPrompt`.

### 5-E. Update `BuildEvaluationPrompt`

Add `string moderationHistoryText` parameter. Add a moderation history block before the transcript and add `ACCURACY` to the rubric:

```csharp
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
```

### 5-F. Update `ParseVerdict` weighted calculation

Add `ACCURACY` to both PRO and CON weighted sums:

```csharp
var proWeighted =
    ReadScore(scores, "PRO_LOGIC")       * spectator.LogicWeight +
    ReadScore(scores, "PRO_REBUTTAL")    * spectator.RebuttalWeight +
    ReadScore(scores, "PRO_CONSISTENCY") * spectator.ConsistencyWeight +
    ReadScore(scores, "PRO_WELLBEING")   * spectator.WellbeingWeight +
    ReadScore(scores, "PRO_PRACTICALITY")* spectator.PracticalityWeight +
    ReadScore(scores, "PRO_ACCURACY")    * spectator.AccuracyWeight;   // ← new

var conWeighted =
    ReadScore(scores, "CON_LOGIC")       * spectator.LogicWeight +
    ReadScore(scores, "CON_REBUTTAL")    * spectator.RebuttalWeight +
    ReadScore(scores, "CON_CONSISTENCY") * spectator.ConsistencyWeight +
    ReadScore(scores, "CON_WELLBEING")   * spectator.WellbeingWeight +
    ReadScore(scores, "CON_PRACTICALITY")* spectator.PracticalityWeight +
    ReadScore(scores, "CON_ACCURACY")    * spectator.AccuracyWeight;   // ← new
```

### 5-G. Add `BuildModerationHistoryText` (new private static method)

```csharp
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
```

---

## File 6 — `backend/Program.cs`

### 6-A. Construct `DebateModerator` in `CreateRuntime`

After the `client` variable is created, before `brain`:

```csharp
var moderator = new DebateModerator(
    client,
    model,
    loggerFactory.CreateLogger<DebateModerator>());
```

### 6-B. Pass `moderator` to `DebateOrchestrator` constructor

```csharp
var orchestrator = new DebateOrchestrator(
    client,
    brain,
    knowledgeStore,
    model,
    loggerFactory.CreateLogger<DebateOrchestrator>(),
    moderator,       // ← new
    debateStyle,
    proTone,
    conTone,
    minTurnDelaySeconds);
```

### 6-C. Add `onModerationComplete` callback in the stream endpoint

In the `ContinueDebateAsync` call inside `/api/debates/stream`, add the new callback parameter before `response.HttpContext.RequestAborted`:

```csharp
var progress = await runtime.Orchestrator!.ContinueDebateAsync(
    session,
    waitForHumanInput: true,
    turn => WriteSseEvent(response, "turn", turn),
    checkpoint => WriteSseEvent(response, "human-question", new
    {
        sessionId = session.SessionId,
        checkpoint
    }),
    turn => WriteSseEvent(response, "moderation", new   // ← new
    {
        round = turn.Round,
        stance = turn.Stance,
        flagged = turn.Moderation?.Flagged ?? false,
        issues = turn.Moderation?.Issues ?? Array.Empty<string>(),
        summary = turn.Moderation?.Summary ?? string.Empty
    }),
    response.HttpContext.RequestAborted);
```

---

## Data Flow

```
ContinueDebateAsync(session)
    │
    └─ while (mainTurns < maxMainTurns):
            │
            ├─ GenerateTurn(...)
            │     → DebateTurn { ..., Moderation = null }
            │
            ├─ transcript.Turns.Add(turn)
            │
            ├─ onTurnGenerated(turn)        ← fires "turn" SSE event (Moderation still null)
            │
            ├─ if turnKind == "argument":   ← NEW
            │     DebateModerator.EvaluateAsync(turn, topic)
            │       ChatCompletionsClient.CompleteAsync(moderatorPrompt)
            │         → JSON { flagged, issues[], summary }
            │       TryParseModerationRaw → ModerationNote
            │       turn.Moderation = note  ← mutates the turn already in transcript
            │       log 4002
            │       onModerationComplete(turn)  ← fires "moderation" SSE event
            │
            └─ (loop continues)

CompleteSessionAsync(session, panel)
    │
    └─ DebateSpectatorPanel.EvaluateAsync(transcript)
            │
            ├─ BuildTranscriptText(transcript)       ← unchanged
            ├─ BuildModerationHistoryText(transcript) ← NEW (reads turn.Moderation)
            │
            └─ foreach spectator:
                    EvaluateSingleSpectator(spectator, topic, transcriptText, moderationHistoryText)
                      prompt includes moderation flags + 6-criteria rubric (ACCURACY added)
                      ParseVerdict includes PRO_ACCURACY + CON_ACCURACY × AccuracyWeight
```

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Moderator adds latency to every main turn | `CompleteAsync` is async; `Temperature=0.2, MaxTokens=150` keeps calls short |
| Moderator throws on every turn (e.g., API outage) | `try/catch` returns `FallbackNote { Flagged=false }` — debate continues |
| LLM ignores JSON format, returns prose | `TryParseModerationRaw` tries brace-extraction and fence-stripping fallbacks |
| `turn.Moderation` is set after `turn` SSE event fires | Intentional — `moderation` event is separate; turn payload is unchanged |
| Spectator weight rebalancing changes existing scores | Weights scaled proportionally; ACCURACY = 0.1 applies equally to PRO and CON |
| `SpectatorProfile` record gains a 6th positional parameter | All 14 catalog entries are updated in the same commit; no external callers |
| `DebateOrchestrator` constructor gains a new required parameter | `moderator` is placed before optional params; only `CreateRuntime` in `Program.cs` constructs it |
| `onModerationComplete` inserted before `cancellationToken` | Named parameter (`cancellationToken`) used at existing call sites — no breakage |

---

## Verification Checklist (manual — no automated tests)

- [ ] `dotnet build backend/BackEnd.csproj -c Release --no-incremental -warnaserror` — 0 errors, 0 warnings
- [ ] Start a 3-round debate; confirm log events 4000×N (one per main turn) appear after turn events 2000
- [ ] Confirm event 4001 is absent under normal conditions
- [ ] Confirm `moderation` SSE events fire after each `turn` event (check browser DevTools / curl output)
- [ ] Confirm a flagged moderation note appears in `transcript.Turns[n].Moderation` in the final `completed` payload
- [ ] Confirm spectator prompts now request `PRO_ACCURACY` / `CON_ACCURACY` (visible in logs if debug logging is enabled)
- [ ] Confirm debate completes normally end-to-end
