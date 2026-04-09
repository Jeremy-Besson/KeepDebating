# TryingStuff

## Overview

TryingStuff is a proof-of-concept solution for building an agentic debate assistant called **KeepDebating**.

The app lets a user provide a debate topic, then runs a structured pro/con discussion between two AI personas. The system is designed to practice real agentic patterns in a practical way:

- orchestration and decision-making,
- tool integration (Wikipedia),
- grounding and reasoning quality,
- human-in-the-loop checkpoints,
- transcript and verdict generation.

## Why This Project

This project focuses on learning by building a complete end-to-end agent workflow, not just single prompt/response demos.

Key goals:

- explore context, memory, and failure handling,
- orchestrate multiple roles (PRO, CON, brain/orchestrator, spectator panel),
- integrate external data/tools,
- keep the stack practical and easy to run locally.

## Solution Structure

- **BackEnd** (`.NET 10 / C#`): debate orchestration, model calls, tool usage, session handling, transcript and verdict pipeline.
- **FrontEnd** (`React + Vite + TypeScript + TailwindCSS`): topic/settings input, debate experience, and status/visualization pages.

## Current Capabilities

- User-defined topic and debate settings.
- Two AI sides (PRO/CON) with distinct persona behavior.
- Brain orchestrator decides the next action: continue debate, ask user, or follow-up.
- Wikipedia-based dynamic retrieval for evidence grounding.
- Human-in-the-loop checkpoint when additional context is needed.
- Spectator verdict aggregation and summary output.

## Implementation Roadmap

The PoC evolves in phases:

1. Fixed-topic baseline debate flow.
2. Brain orchestration with Semantic Kernel.
3. Dynamic Wikipedia integration as a tool.
4. Human-in-the-loop interaction model.
5. RAG + memory enhancements.

## Verification Commands

Use these commands before pushing changes.

### One-command (Windows)

Run from repository root:

```powershell
.\Run-Verify.bat
```

### FrontEnd

Run from `FrontEnd/`:

```powershell
npm ci
npm run verify
```

Equivalent expanded commands:

```powershell
npm run lint
npm run typecheck
npm run build
```

### BackEnd

Run from `BackEnd/`:

```powershell
dotnet restore .\BackEnd.csproj
dotnet build .\BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal
```

### CI Workflows

These same checks are enforced in GitHub Actions:

- `.github/workflows/frontend-ci.yml`
- `.github/workflows/backend-ci.yml`

## Azure Deployment Scaffold

This repository now includes starter files for Azure infrastructure and deployment:

- `infra/main.bicep`
- `infra/parameters/dev.bicepparam`
- `.github/workflows/deploy-backend.yml`
- `.github/workflows/deploy-frontend.yml`
- `FrontEnd/public/staticwebapp.config.json`

### 1. Provision Infrastructure (Dev)

From repository root:

```powershell
az group create --name <your-rg> --location <your-location>
az deployment group create --resource-group <your-rg> --parameters infra/parameters/dev.bicepparam --template-file infra/main.bicep
```

### 2. Configure GitHub Secrets

Set these repository secrets for deployment workflows:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_WEBAPP_NAME`
- `AZURE_STATIC_WEB_APPS_API_TOKEN`

### 3. Configure BackEnd App Settings

In the Azure App Service settings, configure:

- `AzureOpenAI__Endpoint`
- `AzureOpenAI__ApiKey`
- `AzureOpenAI__Model`

### 4. Configure FrontEnd API Route

Update `FrontEnd/public/staticwebapp.config.json` and replace:

- `https://REPLACE_WITH_BACKEND_HOST`

with your deployed App Service host, for example:

- `https://my-app-api-dev.azurewebsites.net`

### 5. Deploy

Push to `main` (or run workflows manually) to execute:

- BackEnd deployment: `.github/workflows/deploy-backend.yml`
- FrontEnd deployment: `.github/workflows/deploy-frontend.yml`
