# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**KeepDebating** is a proof-of-concept agentic AI debate assistant. Two AI personas (PRO/CON) debate a user-supplied topic. A Semantic Kernel "brain" orchestrates turn decisions and calls Wikipedia as a live tool. A human-in-the-loop checkpoint can pause the stream for user input. A 14-persona spectator panel scores the debate at the end.

## Commands

### FrontEnd (run from `FrontEnd/`)
```bash
npm ci                # install locked dependencies
npm run dev           # Vite dev server on port 5173
npm run lint          # ESLint (typescript-eslint + react-hooks)
npm run typecheck     # tsc -b (no emit)
npm run build         # production build
npm run verify        # lint + typecheck + build (CI gate)
npm run lint:fix      # ESLint auto-fix
```

### BackEnd (run from `BackEnd/`)
```bash
dotnet restore .\BackEnd.csproj
dotnet build .\BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal
dotnet run            # dev server on http://localhost:5008
```

### Full verification (from repo root, Windows)
```bat
.\Run-Verify.bat
```

There are no automated test projects — verification is build + lint + typecheck only.

## Architecture

### Stack
- **BackEnd:** .NET 10 Minimal API (no MVC controllers). All endpoints and DTOs are defined in `Program.cs`. Dev port: `5008`.
- **FrontEnd:** React 18 + TypeScript, Vite 8, TailwindCSS 3, react-router-dom 7. Dev port: `5173`.

### BackEnd Services

| Service | Role |
|---|---|
| `DebateBrainOrchestrator` | Semantic Kernel with `FunctionChoiceBehavior.Auto()`. Decides next action (`next-turn`, `ask-user`) and calls `WikipediaPlugin` automatically. Falls back to strict PRO/CON alternation on parse failure. |
| `DebateOrchestrator` | Drives the debate loop turn by turn using `Azure AI Inference` (`ChatCompletionsClient`). Handles content-filter retries. |
| `WikipediaPlugin` | Semantic Kernel `[KernelFunction]` plugin that calls Wikipedia Search + Summary APIs. Per-session dedup cache. |
| `DebateSpectatorPanel` | Runs up to 14 spectator personas sequentially, each scoring 5 weighted criteria. Computes a weighted winner. |
| `DebateSessionStore` | `ConcurrentDictionary`-backed in-memory session registry. Uses `TaskCompletionSource<string>` for the human-in-the-loop SSE pause/resume. |
| `DebaterCatalog` | Static catalog of 12 debater personas. |

### FrontEnd
- `src/api.ts` — all HTTP/SSE calls to the backend
- `src/types.ts` — TypeScript types mirroring backend DTOs
- `src/pages/DebatePage.tsx` — main debate UI (streaming, human-loop, spectator verdicts)
- `src/pages/IntroPage.tsx` — explainer page with Mermaid architecture diagram

### Frontend–Backend Communication

**Development:** Vite proxies `/api/*` to `http://localhost:5008` (configured in `vite.config.ts`). Backend CORS whitelists `http://localhost:5173`.

**Production (Azure):** `FrontEnd/public/staticwebapp.config.json` rewrites `/api/*` to the deployed App Service host.

**Two communication modes:**

1. **REST** — `POST /api/debates/run`, `POST /api/debates/answer`, `GET /api/debaters`, `GET /api/health`
2. **SSE (primary real-time path)** — `GET /api/debates/stream?topic=...` streams events: `started`, `turn`, `human-question`, `verdict`, `completed`, `stream-error`

Human-in-the-loop bridges both: the SSE stream suspends on a `TaskCompletionSource`, the frontend calls `POST /api/debates/answer`, and the stream resumes.

### Key Patterns
- **Prompt injection defense:** All user content fed into prompts is sanitized via `SanitizePromptValue` / `SanitizeHistoryText` (strips code fences and jailbreak phrases).
- **Content filter retry:** On Azure `content_filter` error, the orchestrator retries once with a safe prompt at lower temperature.
- **Strict alternation fallback:** If Semantic Kernel or JSON parsing fails, `StrictAlternationFallback` returns a deterministic PRO/CON decision so the debate always continues.

### Infrastructure
Azure deployment via `infra/main.bicep`: App Service (BackEnd), Static Web Apps (FrontEnd), Key Vault (RBAC), Application Insights + Log Analytics. Azure OpenAI credentials are supplied via User Secrets or environment variables — never stored in `appsettings.json`.
