# Pull Request: Multi-Agent Moderator

## Conventional Commit Message

```
feat(debate): add dedicated moderator agent that annotates turns and informs spectator scoring
```

## Summary

Introduces a `DebateModerator` agent that evaluates every main debater turn for logical fallacies, off-topic arguments, and unsupported factual claims. Its annotation is attached to the turn, streamed as a new `moderation` SSE event, and fed into spectator scoring via a new `ACCURACY` criterion — closing the feedback loop between agents.

### Changes

| File | Change |
|---|---|
| `backend/Models/ModerationNote.cs` | **New.** `Flagged`, `Issues[]`, `Summary` model |
| `backend/Models/DebateTurn.cs` | Added `ModerationNote? Moderation { get; set; }` |
| `backend/Services/DebateModerator.cs` | **New.** `EvaluateAsync`, JSON parse with fallbacks, log events 4000/4001, static fallback note |
| `backend/Services/DebateOrchestrator.cs` | Injected `DebateModerator`, log event 4002, `onModerationComplete` callback param, moderation block after every main turn |
| `backend/Services/DebateSpectatorPanel.cs` | 6th criterion `ACCURACY`, all 14 spectator weights rebalanced (existing × 0.9 + 0.1), moderation history block in prompt, `BuildModerationHistoryText` helper |
| `backend/Program.cs` | Constructs `DebateModerator`, passes it to orchestrator, emits `moderation` SSE event per main turn |

## How to Test

1. Start the backend: `dotnet run` from `BackEnd/`
2. Start a 3-round debate via the SSE stream endpoint (`GET /api/debates/stream?topic=...`)
3. Check structured logs for:
   - **Event 4000** (`LogModerationComplete`) — one per main turn, appearing after event 2000
   - **Event 4001** (`LogModerationFailed`) — should be absent under normal conditions
   - **Event 4002** (`LogModerationAttached`) — one per main turn at Debug level
4. Check the SSE stream (browser DevTools / curl) for `moderation` events interleaved after each `turn` event
5. Confirm a `moderation` note (including `flagged`, `issues`, `summary`) appears in `transcript.turns[n].moderation` in the final `completed` payload
6. Confirm debate completes normally end-to-end with spectator verdicts

## Verification

```
dotnet build backend/BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal
```

Result: **0 errors, 0 warnings**
