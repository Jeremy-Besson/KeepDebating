# Plan: Plan then Execute

**Phase B — Technical Blueprint**

---

## Status
- [x] Phase A: Analysis
- [x] Phase B: Planning
- [ ] Phase C: Implementation, Testing & PR

---

## Scope

Four files change. One new model file created.

| File | Change type |
|---|---|
| `backend/Models/DebatePlan.cs` | New file |
| `backend/Services/DebateSessionStore.cs` | Add one field to `DebateSessionState` |
| `backend/Services/DebateBrainOrchestrator.cs` | Add `PlanDebateAsync`, two log delegates, parse helpers, brain prompt update, `BrainDecisionContext` field |
| `backend/Services/DebateOrchestrator.cs` | Add one log delegate, call `PlanDebateAsync` before loop, inject `PlannedArgumentType` per turn |

---

## File 1 — `backend/Models/DebatePlan.cs` (new file)

```csharp
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
```

---

## File 2 — `backend/Services/DebateSessionStore.cs`

Add one mutable field to `DebateSessionState` (after `IsCompleted`, line ~105):

```csharp
public bool IsCompleted { get; set; }
public DebatePlan? Plan { get; set; }   // ← new
```

No other changes to this file.

---

## File 3 — `backend/Services/DebateBrainOrchestrator.cs`

### 3-A. Two new log delegates (after `LogHumanQuestionGenerationFailed`, line ~36)

```csharp
private static readonly Action<ILogger, string, string, Exception?> LogDebatePlanCreated =
    LoggerMessage.Define<string, string>(
        LogLevel.Information,
        new EventId(3000, nameof(LogDebatePlanCreated)),
        "Debate plan created. Stance: {Stance}, Strategy: {Strategy}");

private static readonly Action<ILogger, string, string, Exception?> LogDebatePlanFailed =
    LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(3001, nameof(LogDebatePlanFailed)),
        "Debate plan failed. Stance: {Stance}, Reason: {Reason}");
```

### 3-B. New public method: `PlanDebateAsync`

Place after `DecideAsync` (after line ~143), before `GenerateHumanQuestionAsync`.

```csharp
public async Task<StancePlan?> PlanDebateAsync(
    string topic,
    int rounds,
    string stance,
    CancellationToken cancellationToken = default)
{
    var safeTopic = topic.Replace("\"", "'");

    var prompt = $"""
        You are creating a debate strategy for a {stance} debater.
        Topic: "{safeTopic}"
        Rounds: {rounds}

        For each round (1 to {rounds}), assign one argument type that best fits this topic and stance.
        Choose freely — e.g., statistical, emotional, philosophical, ethical, analogical, narrative, rebuttal, etc.
        Vary the types across rounds for a dynamic, unpredictable debate.

        Return strict JSON only:
        {{
          "strategy": [
            {{ "round": 1, "argumentType": "statistical" }},
            {{ "round": 2, "argumentType": "emotional" }}
          ]
        }}
        """;

    try
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var settings = new AzureOpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            TopP = 0.9,
            MaxTokens = 200
        };

        var reply = await _chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            settings,
            _kernel,
            cancellationToken);

        var raw = reply.Content ?? string.Empty;

        if (!TryParseStrategyRaw(raw, out var strategyRaw) || strategyRaw?.Strategy is null)
        {
            LogDebatePlanFailed(_logger, stance, "Unparsable JSON response", null);
            return null;
        }

        var roundPlans = strategyRaw.Strategy
            .Where(r => r.Round > 0 && !string.IsNullOrWhiteSpace(r.ArgumentType))
            .Select(r => new RoundPlan { Round = r.Round, ArgumentType = r.ArgumentType!.Trim() })
            .ToArray();

        var stancePlan = new StancePlan { Stance = stance, Rounds = roundPlans };
        var strategyStr = string.Join(", ", roundPlans.Select(r => $"R{r.Round}:{r.ArgumentType}"));
        LogDebatePlanCreated(_logger, stance, strategyStr, null);

        return stancePlan;
    }
    catch (Exception ex)
    {
        LogDebatePlanFailed(_logger, stance, ex.GetType().Name, ex);
        return null;
    }
}
```

### 3-C. New private parse helpers

Add `TryParseStrategyRaw` after `TryParseDecisionRaw` (after line ~225):

```csharp
private static bool TryParseStrategyRaw(string raw, out StrategyRaw? parsed)
{
    parsed = null;

    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    foreach (var candidate in BuildParseCandidates(raw))
    {
        try
        {
            parsed = JsonSerializer.Deserialize<StrategyRaw>(candidate, options);
            if (parsed is not null)
            {
                return true;
            }
        }
        catch (JsonException) { }
    }

    return false;
}
```

Add two private sealed classes after `BrainDecisionRaw` (after line ~391):

```csharp
private sealed class StrategyRaw
{
    public IReadOnlyList<RoundPlanRaw>? Strategy { get; init; }
}

private sealed class RoundPlanRaw
{
    public int Round { get; init; }
    public string? ArgumentType { get; init; }
}
```

### 3-D. Update brain system prompt in `DecideAsync` (line ~89)

Add one line to the policy constraints block:

```
// Before (existing line):
- You have a Wikipedia tool. Use it when evidence is thin or stale and choose precise search queries.

// After:
- You have a Wikipedia tool. Use it when evidence is thin or stale and choose precise search queries.
- If "plannedArgumentType" is set in the debate state, prefer that rhetorical style for this turn — but deviate if the debate flow calls for it.
```

### 3-E. Update `BrainDecisionContext` (line ~396)

Add `using System.Text.Json.Serialization;` at the top of the file.

Add one nullable field at the end of `BrainDecisionContext`:

```csharp
public string? LastHumanAnswer { get; init; }

[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? PlannedArgumentType { get; init; }   // ← new
```

`WhenWritingNull` keeps the brain JSON compact when no plan exists — the field is simply absent from the serialized context.

---

## File 4 — `backend/Services/DebateOrchestrator.cs`

### 4-A. New log delegate (after `LogRagRetrieveFailed` / `LogReflectionGenerated`, line ~57)

```csharp
private static readonly Action<ILogger, int, string, string, Exception?> LogPlanGuidanceInjected =
    LoggerMessage.Define<int, string, string>(
        LogLevel.Information,
        new EventId(3002, nameof(LogPlanGuidanceInjected)),
        "Plan guidance injected. Round: {Round}, Stance: {Stance}, ArgumentType: {ArgumentType}");
```

### 4-B. Update `ContinueDebateAsync`

**Before the while loop** (after `pro`/`con` profiles are created, before `CountMainTurns`):

```csharp
// Run both planning calls in parallel — non-fatal if either fails.
var planningResults = await Task.WhenAll(
    _brain.PlanDebateAsync(transcript.Topic, transcript.Rounds, "PRO", cancellationToken),
    _brain.PlanDebateAsync(transcript.Topic, transcript.Rounds, "CON", cancellationToken));

var proPlan = planningResults[0];
var conPlan = planningResults[1];

if (proPlan is not null || conPlan is not null)
{
    session.Plan = new DebatePlan
    {
        Pro = proPlan ?? new StancePlan { Stance = "PRO", Rounds = [] },
        Con = conPlan ?? new StancePlan { Stance = "CON", Rounds = [] }
    };
}
```

**Inside the while loop** — before the `DecideAsync` call, add:

```csharp
var plannedArgumentType = session.Plan?.GetArgumentType(expectedStance, round);
if (plannedArgumentType is not null)
{
    LogPlanGuidanceInjected(_logger, round, expectedStance, plannedArgumentType, null);
}
```

**In the `BrainDecisionContext` initializer** — add `PlannedArgumentType`:

```csharp
var decision = await _brain.DecideAsync(new BrainDecisionContext
{
    Topic = transcript.Topic,
    Round = round,
    MainTurnsCompleted = mainTurnsCompleted,
    MaxMainTurns = mainTurnTarget,
    ExpectedStance = expectedStance,
    CanUseFollowUp = canUseFollowUp,
    HasAskedHumanFeedback = transcript.HumanLoopCheckpoints.Count > 0,
    LastTurnStance = lastTurn?.Stance,
    LastTurnMessage = lastTurn?.Message,
    RecentTurns = transcript.Turns.TakeLast(6).ToArray(),
    LastHumanAnswer = session.LastHumanAnswer,
    PlannedArgumentType = plannedArgumentType   // ← new
}, cancellationToken);
```

---

## Data Flow

```
ContinueDebateAsync(session)
    │
    ├─ CreateDebaterProfile(Pro), CreateDebaterProfile(Con)
    │
    ├─ Task.WhenAll(                                      ← NEW
    │     PlanDebateAsync(topic, rounds, "PRO")           ← NEW
    │     PlanDebateAsync(topic, rounds, "CON")           ← NEW
    │   )
    │     each call:
    │       ChatHistory.AddUserMessage(strategyPrompt)
    │       _chatCompletion (Temp=0.7, TopP=0.9, MaxTokens=200, no tools)
    │       TryParseStrategyRaw → StancePlan { Stance, Rounds[] }
    │       on failure → null (logged 3001)
    │
    ├─ session.Plan = DebatePlan { Pro=proPlan, Con=conPlan }  ← NEW
    │
    └─ while (mainTurns < maxMainTurns):
            │
            ├─ plannedArgumentType = session.Plan?.GetArgumentType(stance, round)  ← NEW
            ├─ log 3002 if non-null                                                 ← NEW
            │
            ├─ DecideAsync(BrainDecisionContext { ..., PlannedArgumentType })       ← UPDATED
            │     brain JSON includes "plannedArgumentType": "statistical"
            │     brain policy: "prefer that style — but deviate if needed"
            │
            └─ GenerateTurn(...)
```

---

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Two extra LLM calls add pre-debate latency | Both run in parallel via `Task.WhenAll`; each is short (MaxTokens 200) |
| Planner picks nonsensical type for round 1 (e.g., "rebuttal") | Soft guidance only — brain naturally adapts; no hard enforcement |
| JSON parse fails for one stance | Non-fatal; partial plan stored with empty `Rounds` for failed stance |
| `[JsonIgnore(WhenWritingNull)]` not available on `BrainDecisionContext` | Requires `using System.Text.Json.Serialization;` — straightforward |
| `DebatePlan` required fields: Pro and Con are `required` | Handled by fallback to empty `StancePlan` when one planning call fails |
| `Task.WhenAll` propagates exceptions from tasks | `PlanDebateAsync` catches all exceptions internally and returns null — `Task.WhenAll` never throws |

---

## Verification Checklist (manual — no automated tests)

- [ ] `dotnet build ./BackEnd.csproj -c Release --no-incremental -warnaserror` — 0 errors, 0 warnings
- [ ] Start a 3-round debate; confirm log events 3000×2 (one PRO, one CON) appear before the first turn event 2000
- [ ] Confirm `plannedArgumentType` appears in brain JSON log (event 3002) for each turn where the plan covers the round
- [ ] Confirm turns beyond the plan's length get no 3002 log (null plan guidance)
- [ ] Confirm debate completes normally end-to-end
