# Feature: Debater Reflection (ReAct Pattern)

**Slug:** `debater-reflection-react`  
**Description:** Before each debater turn (after their first), a dedicated reflection LLM call assesses the strength of their last argument and injects self-critique into the next prompt — implementing the ReAct (Reason + Act) pattern.

---

## Refined Requirements

### Core Behaviour

- [ ] Before generating each debater turn, check whether that speaker has a prior turn in the transcript.
- [ ] If a prior turn exists, call the LLM to generate a short reflection (2–3 sentences) from that speaker's perspective assessing:
  - The strongest point they made in their previous argument.
  - Whether they addressed the opponent's last point — and what they missed if not.
  - The most important gap or weakness to correct in their next argument.
- [ ] Inject the reflection into `BuildUserPrompt` as a new block between the facts block and the instructions block.
- [ ] If no prior turn exists for that speaker (first turn of the debate), skip reflection silently — no placeholder injected.
- [ ] Reflection is non-fatal: if the reflection LLM call fails (exception, content filter, timeout), log a warning and continue with the turn without reflection. The debate must never be blocked by reflection failure.

---

### Prompt Design

**Reflection system message:** reuse the speaker's existing `SystemPrompt` (same persona/stance).

**Reflection user message:**
```
Your previous argument in this debate was:
"{sanitized previousTurn.Message}"

The opponent's most recent argument was:
"{sanitized lastOpponentTurn.Message}"

In 2–3 sentences, reflect honestly on your previous argument:
- What was the strongest point you made?
- Did you directly address the opponent's argument? If not, what did you miss?
- What one gap or weakness should you correct in your next argument?

Do not write the next argument — only reflect.
```

**Injected block in `BuildUserPrompt` (after the facts block, before instructions):**
```
Self-reflection on your previous argument:
{reflection}

Use this reflection to improve your next argument — address what you missed and reinforce what worked.
```

---

### LLM Call Parameters

- Reuse `CompleteTurnMessage` (or a thin `GenerateReflection` wrapper around the same `_client.Complete` path).
- Temperature: `0.4`, TopP: `0.8`, MaxTokens: `150`.
- No retry loop — a single attempt only. On failure, skip reflection (non-fatal).
- Same `ChatCompletionsClient` / Azure AI Inference — no new external dependency.

---

### Data Model

- Add optional `string? ReflectionText` to `DebateTurn` for observability and logging.
  - Populated when a reflection was generated; `null` when skipped (first turn, or failure).
- `ToolFactsUsed` is unchanged — reflection is reasoning, not a sourced fact.

---

### Code Changes

| File | Change |
|---|---|
| `backend/Services/DebateOrchestrator.cs` | Add `GenerateReflection(speaker, history)` private method. Update `GenerateTurn` to call it and pass result to `BuildUserPrompt`. Update `BuildUserPrompt` signature to accept optional `reflection`. |
| `backend/Models/DebateTurn.cs` | Add `string? ReflectionText { get; init; }`. |
| `backend/Services/DebateOrchestrator.cs` | Sanitize reflection output via `SanitizePromptValue` before injection (it is LLM output going back into a prompt). |

No changes needed to: `DebateBrainOrchestrator`, `WikipediaPlugin`, `DebateKnowledgeStore`, `Program.cs`, frontend.

---

### Edge Cases

| Case | Handling |
|---|---|
| First turn for this speaker | No prior turn found in history → skip reflection, proceed normally |
| Follow-up turn (same speaker twice) | Prior turn is the immediately preceding one — reflection is most valuable here, apply normally |
| Reflection call hits content filter | Catch exception, log warning (Event ID 2006), set `reflection = null`, continue |
| Previous turn was a canned fallback message | Reflection would be meaningless. Detect by checking `turn.Message` against known fallback strings; skip if matched |
| Reflection output contains injection attempt | Sanitize via `SanitizePromptValue` before injecting into next prompt |
| Both speakers have no prior turns (round 1) | Both skip — no reflection on round 1, normal behaviour |

---

### Observability

| Event ID | Logger | Message | When |
|---|---|---|---|
| 2006 | DebateOrchestrator | `Reflection skipped. Round: {Round}, Stance: {Stance}, Reason: {Reason}` | No prior turn, failure, or fallback detected |
| 2007 | DebateOrchestrator | `Reflection generated. Round: {Round}, Stance: {Stance}, Reflection: {Reflection}` | Successful reflection call |

---

### Out of Scope

- Showing reflection text in the frontend SSE stream or UI (internal reasoning step only).
- Generating reflection for spectators.
- Storing reflection in the `DebateTranscript` beyond the `DebateTurn.ReflectionText` field.
- Configuring reflection on/off via `appsettings.json` (not needed for a proof-of-concept).

---

### Open Questions

1. **Opponent turn lookup:** For the reflection prompt, "the opponent's most recent argument" = last turn whose `Stance != speaker.Stance`. If no opponent turn exists yet (very first CON turn), omit that line from the reflection prompt entirely.
2. **`_proTone` / `_conTone`** are currently unused dead config fields. Should they be injected into the reflection prompt as tone guidance? (Deferred — out of scope for this feature.)
