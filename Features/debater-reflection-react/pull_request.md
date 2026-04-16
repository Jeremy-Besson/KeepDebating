# Pull Request: Debater Reflection (ReAct Pattern)

## Conventional Commit

```
feat(debate): add ReAct reflection step before each debater turn
```

---

## Summary

Implements the **ReAct (Reason + Act)** pattern for debate turns.

Before generating each argument, the debater runs a dedicated reflection LLM call that assesses their previous turn — what was strong, what was missed, and what gap to correct. The reflection is injected into the debater's next prompt, forcing the agent to reason about its own output before acting again.

- First turn for each speaker: reflection is skipped (no prior turn to reflect on).
- Reflection failure (exception, content filter): logged at Warning (event 2006), debate continues unaffected.
- Successful reflection: logged at Information (event 2007), stored on `DebateTurn.ReflectionText`.

---

## Modified Files

| File | Change |
|---|---|
| `backend/Models/DebateTurn.cs` | Added `string? ReflectionText { get; init; }` |
| `backend/Services/DebateOrchestrator.cs` | Added `GenerateReflection`, `CompleteReflectionMessage`; updated `GenerateTurn`, `BuildUserPrompt`; added log events 2006/2007 |

---

## How to Test

1. Start the backend: `dotnet run` from `backend/`
2. Start a debate via the frontend (or curl `GET /api/debates/stream?topic=...`)
3. **Turns 1 & 2 (PRO and CON first turns):** Check server logs for event 2006 — `Reflection skipped ... Reason: no prior turn`
4. **Turn 3 onwards:** Check server logs for event 2007 — `Reflection generated ... Reflection: <text>`
5. Inspect SSE `turn` events: the JSON payload now includes a `reflectionText` field — `null` for first-speaker turns, a non-null string for subsequent ones
6. Observe whether later arguments are more responsive to the opponent's previous point

---

## Verification

- [x] `dotnet build ./BackEnd.csproj -c Release --no-incremental -warnaserror` — 0 errors, 0 warnings
- [ ] Manual: turns 1 & 2 log reflection skipped (event 2006)
- [ ] Manual: turn 3+ log reflection generated (event 2007)
- [ ] Manual: `reflectionText` appears in SSE turn JSON (null / string as expected)
- [ ] Manual: debate completes normally end-to-end
