# Requirements: Multi-Agent Moderator

**Phase A — Analysis**

---

## Status
- [x] Phase A: Analysis
- [ ] Phase B: Planning
- [ ] Phase C: Implementation, Testing & PR

---

## Feature Name
`multi-agent-moderator`

## Description
A dedicated Moderator agent evaluates each main debater turn for logical fallacies, off-topic drift, and unsupported claims. Its annotation is attached to the turn and streamed to the client as a `moderation` SSE event. Spectator scoring gains a sixth criterion — `ACCURACY` — informed by the moderator's history.

---

## Design Decisions (confirmed)

| Question | Decision |
|---|---|
| Debate flow impact | **Annotate only** — moderator note attached to turn and streamed; debate never pauses |
| Spectator integration | **New criterion** — add `ACCURACY` (6th) with its own per-spectator weight |
| Trigger | **After every main turn** — unconditional; follow-up turns are not moderated |
| LLM stack | **Azure AI Inference (`ChatCompletionsClient`)** — same as debaters and spectators |

---

## Must-Have Requirements

### 1. `ModerationNote` model (new file `backend/Models/ModerationNote.cs`)
- `bool Flagged` — `true` if the moderator identified at least one issue
- `string[] Issues` — zero or more short labels (e.g. `"ad hominem"`, `"unsupported claim"`, `"off-topic"`)
- `string Summary` — one-sentence moderator verdict (populated even when `Flagged = false`)

### 2. `DebateTurn` model update
- Add optional field: `ModerationNote? Moderation` — `null` until the moderator runs; set immediately after evaluation

### 3. `DebateModerator` service (new file `backend/Services/DebateModerator.cs`)
- Depends on `ChatCompletionsClient` and `string model` (same client as debaters)
- Dedicated neutral system prompt — moderator persona, no stance, no alignment
- Single public method: `Task<ModerationNote> EvaluateAsync(DebateTurn turn, string topic, CancellationToken)`
- Evaluates for three failure modes:
  - **Logical fallacies** (ad hominem, straw man, false dichotomy, slippery slope, etc.)
  - **Off-topic arguments** (argument does not address the debate topic)
  - **Unsupported factual claims** (assertion of statistics or facts with no grounding in the provided facts)
- Returns strict JSON: `{ "flagged": bool, "issues": ["..."], "summary": "..." }`
- **Non-fatal**: any exception or parse failure returns `ModerationNote { Flagged = false, Issues = [], Summary = "Moderation unavailable." }`
- Log delegate (EventId 4000): `"Moderation complete. Round: {Round}, Stance: {Stance}, Flagged: {Flagged}, Issues: {Issues}"`
- Log delegate (EventId 4001): `"Moderation failed. Round: {Round}, Stance: {Stance}, Reason: {Reason}"` (warning, non-fatal)

### 4. `DebateOrchestrator` update
- Inject `DebateModerator` via constructor
- After generating each **main turn** (not follow-up turns): call `EvaluateAsync`, attach result to `turn.Moderation`
- Fire a new `moderation` SSE event immediately after the `turn` SSE event

### 5. New SSE event: `moderation`
- Emitted right after the `turn` event for every main turn
- Payload fields:
  - `round` (int)
  - `stance` (string: `"PRO"` | `"CON"`)
  - `flagged` (bool)
  - `issues` (string[])
  - `summary` (string)

### 6. `DebateSpectatorPanel` update — 6th criterion: `ACCURACY`
- Add `AccuracyWeight` to `SpectatorProfile`
- Rebalance all 14 spectator catalog entries: scale existing 5 weights by `0.9`, set `AccuracyWeight = 0.1` for all spectators
- Update `BuildEvaluationPrompt` to:
  - Include a moderation history block listing all flagged turns (stance + issues) before the transcript
  - Add `ACCURACY` to the 5-criteria rubric description: *"how well the debater avoided logical fallacies, stayed on topic, and grounded claims in evidence"*
  - Add `PRO_ACCURACY` and `CON_ACCURACY` to the required key-value output (13 lines total)
- Update `ParseVerdict` and weighted score calculation to include `ACCURACY`
- `DebateTranscript` must be passed (not just the text string) so moderation history is accessible

### 7. `Program.cs` registration
- Register `DebateModerator` as a singleton (or scoped per session factory pattern matching existing services)
- Inject into `DebateOrchestrator` construction site

---

## Constraints

- **No breaking changes to existing SSE event shapes** — `turn`, `verdict`, `completed` payloads are unchanged; `moderation` is additive
- **Non-fatal everywhere** — moderator failure must never stop the debate
- **Follow-up turns are not moderated** — only `TurnKind == "argument"` turns are evaluated
- **Spectator weight rebalancing** — existing 5 weights × 0.9 must still sum with `AccuracyWeight` to exactly 1.0 per spectator
- **No frontend changes required** — the frontend may ignore unknown SSE event types gracefully

---

## Edge Cases

| Case | Handling |
|---|---|
| Moderator LLM call times out or throws | Catch all exceptions → fallback `ModerationNote { Flagged = false }`, log 4001 |
| Moderator returns unparsable JSON | Same fallback + log 4001 |
| Moderator flags both sides on every turn | Expected; scoring absorbs it via `ACCURACY` criterion |
| A follow-up turn is generated | Skip moderator entirely; `turn.Moderation` remains `null` |
| Spectator sees no flagged turns | Moderation block in prompt shows "No moderation flags were raised." |
| `DebateTranscript` passed to spectator already complete | Moderation notes are on `Turns` — no extra data structure needed |

---

## Out of Scope

- Frontend UI changes for displaying moderation events
- Moderator using Wikipedia or other tools (deferred; annotate-only with current facts context is sufficient)
- Per-session enable/disable toggle (all sessions are moderated)
- Hard interrupts or debate-flow changes based on moderation
