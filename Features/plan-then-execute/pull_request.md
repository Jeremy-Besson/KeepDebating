# Pull Request: Plan-Then-Execute Debate Strategy

## Conventional Commit Message

```
feat(debate): add pre-debate planning step to guide argument types per round
```

## Summary

Introduces a "plan-then-execute" pattern to the debate engine. Before the debate loop starts, the `DebateBrainOrchestrator` generates a per-round argument strategy for both PRO and CON stances in parallel. Each turn, the planned argument type (e.g., "statistical", "emotional", "philosophical") is injected into the `BrainDecisionContext` so the orchestration brain can prefer that rhetorical style while remaining free to deviate if the debate flow demands it.

### Changes

| File | Change |
|---|---|
| `backend/Models/DebatePlan.cs` | **New.** `RoundPlan`, `StancePlan`, `DebatePlan` models |
| `backend/Services/DebateSessionStore.cs` | Added `DebatePlan? Plan` field to `DebateSessionState` |
| `backend/Services/DebateBrainOrchestrator.cs` | Added `PlanDebateAsync`, log delegates 3000/3001, `TryParseStrategyRaw`, `StrategyRaw`/`RoundPlanRaw` inner classes, brain prompt policy line, `PlannedArgumentType` field on `BrainDecisionContext` |
| `backend/Services/DebateOrchestrator.cs` | Added log delegate 3002, parallel `Task.WhenAll` planning before loop, `plannedArgumentType` injection inside loop, `PlannedArgumentType` wired into `BrainDecisionContext` |

## How to Test

1. Start the backend: `dotnet run` from `BackEnd/`
2. Start a debate with 3+ rounds via the frontend or `POST /api/debates/stream`
3. Check structured logs for:
   - **Event 3000** (`LogDebatePlanCreated`) × 2 — one PRO, one CON — appearing **before** the first turn event 2000
   - **Event 3001** (`LogDebatePlanFailed`) should be absent under normal conditions
   - **Event 3002** (`LogPlanGuidanceInjected`) — one per main turn where `session.Plan` covers the round
4. Confirm turns beyond the plan's round count produce no 3002 log
5. Confirm the debate completes normally end-to-end (spectator verdicts appear)

## Verification

```
dotnet build backend/BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal
```

Result: **0 errors, 0 warnings**
