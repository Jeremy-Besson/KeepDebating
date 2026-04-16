# Feature: Plan then Execute

**Slug:** `plan-then-execute`  
**Description:** Before the debate starts, the brain generates an independent per-stance multi-round strategy — argument types chosen freely by the LLM. The plan is stored in session state and injected as soft guidance into the brain's decision context each turn, separating planning from execution.

---

## Refined Requirements

### Core Behaviour

- [ ] `PlanDebateAsync` runs once per debate, before the first turn in `ContinueDebateAsync`.
- [ ] It generates two independent plans: one for PRO, one for CON.
- [ ] Each plan is a list of argument types — one per round — chosen freely by the LLM based on the topic and stance (e.g., statistical, emotional, philosophical, ethical, analogical, narrative, rebuttal — no fixed vocabulary).
- [ ] The plan is stored in `DebateSessionState` as `DebatePlan? Plan`.
- [ ] On each brain decision, the planned argument type for the current round and active speaker's stance is looked up and injected into `BrainDecisionContext` as `PlannedArgumentType?`.
- [ ] The brain treats the planned type as **soft guidance** — it can deviate if the debate context calls for it.
- [ ] Planning is **non-fatal**: if `PlanDebateAsync` fails (exception, parse failure, content filter), `session.Plan` stays `null` and the debate proceeds reactively as before.
- [ ] The plan is **internal only** — not emitted as an SSE event, not stored in `DebateTranscript`, not shown in the frontend.

---

### Planner Prompt Design

**System message:** neutral strategist (not PRO or CON persona).

**User message (called twice — once per stance):**
```
You are creating a debate strategy for a {stance} debater.
Topic: "{sanitizedTopic}"
Rounds: {rounds}

For each round (1 to {rounds}), assign one argument type that best fits this topic and stance.
Choose freely — e.g., statistical, emotional, philosophical, ethical, analogical, narrative, rebuttal, etc.
Vary the types across rounds for a dynamic, unpredictable debate.

Return strict JSON only:
{
  "strategy": [
    { "round": 1, "argumentType": "statistical" },
    { "round": 2, "argumentType": "emotional" }
  ]
}
```

**LLM parameters:** Temperature `0.7`, TopP `0.9`, MaxTokens `200`.  
No retry — single attempt. On failure, return `null` for that stance's plan.

---

### Brain Integration

`BrainDecisionContext` gains one new field:

```csharp
public string? PlannedArgumentType { get; init; }
```

Populated from `session.Plan?.GetPlanForStance(stance)?[round]?.ArgumentType`. Null when:
- No plan exists (planning failed)
- The round is beyond the plan's length
- Planning was skipped

The brain system prompt adds one line of guidance:

```
If "plannedArgumentType" is set in the context, prefer that rhetorical style for this turn — but deviate freely if the debate flow calls for it.
```

---

### New DTOs

**`backend/Models/DebatePlan.cs`** (new file):

```csharp
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

### Session State Change

Add to `DebateSessionState`:

```csharp
public DebatePlan? Plan { get; set; }
```

Mutable — set once by `PlanDebateAsync` before the loop, then read-only.

---

### Code Changes

| File | Change |
|---|---|
| `backend/Models/DebatePlan.cs` | New file: `RoundPlan`, `StancePlan`, `DebatePlan` |
| `backend/Services/DebateSessionStore.cs` | Add `DebatePlan? Plan { get; set; }` to `DebateSessionState` |
| `backend/Services/DebateBrainOrchestrator.cs` | Add `PlanDebateAsync(string topic, int rounds, string stance)` method; add `PlannedArgumentType?` to `BrainDecisionContext`; update brain system prompt to reference it |
| `backend/Services/DebateOrchestrator.cs` | Call `PlanDebateAsync` for PRO and CON before the loop; pass `PlannedArgumentType` in `BrainDecisionContext` each turn |

No changes to: `WikipediaPlugin`, `DebateKnowledgeStore`, `DebateSpectatorPanel`, `Program.cs`, frontend.

---

### Edge Cases

| Case | Handling |
|---|---|
| `PlanDebateAsync` throws or times out | Log warning; `session.Plan = null`; debate proceeds reactively |
| Planner JSON parse fails | Treat as no plan; non-fatal |
| Plan has fewer rounds than debate | `GetArgumentType` returns `null` for missing rounds; brain proceeds without guidance |
| Round 1 assigned "rebuttal" | Soft guidance only — brain naturally won't do a full rebuttal with nothing to rebut |
| Human answer mid-debate changes direction | Plan is static but soft — brain can freely deviate |
| Follow-up turn | Inherits the same round's planned type |
| Both PRO and CON planning calls fail | Debate runs fully reactively — no regression |

---

### Observability

| Event ID | Logger | Message | When |
|---|---|---|---|
| 3000 | DebateBrainOrchestrator | `Debate plan created. Stance: {Stance}, Strategy: {Strategy}` | Successful plan |
| 3001 | DebateBrainOrchestrator | `Debate plan failed. Stance: {Stance}, Reason: {Reason}` | Exception or parse failure |
| 3002 | DebateOrchestrator | `Plan guidance injected. Round: {Round}, Stance: {Stance}, ArgumentType: {ArgumentType}` | When `PlannedArgumentType` is non-null |

---

### Out of Scope

- Showing the plan in the frontend or SSE stream.
- Adapting the plan mid-debate based on human answers.
- Storing the plan in `DebateTranscript`.
- Configuring planning on/off via `appsettings.json`.
- Validating or constraining the LLM's argument type vocabulary.
