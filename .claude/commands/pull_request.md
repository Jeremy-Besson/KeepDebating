---
description: "Final Phase: Git Branching, Committing, and PR Creation"
name: "Create PR"
argument-hint: "Feature Name"
agent: "agent"
---

## 1. Status Initialization
- [x] **Phase A: Analysis**
- [x] **Phase B: Planning**
- [x] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Final - Pull Request Creation Start

---

## 2. Instructions

1. Read `Features/$ARGUMENTS/pull_request.md` for the commit message and description.
2. Create branch `feature/$ARGUMENTS` (skip if already on that branch).
3. Stage all relevant changes, including the `Features/$ARGUMENTS/` directory (requirements.md, plan.md, pull_request.md) alongside any modified source files.
4. Commit using the Conventional Commit message from `pull_request.md`.
5. If `gh` CLI is available: push the branch and create a draft PR using the description from `pull_request.md`.
6. If `gh` is not available: output the exact `git push` and `gh pr create` commands for the user to run manually.

---

## 3. Final Status
- [x] **Phase A: Analysis**
- [x] **Phase B: Planning**
- [x] **Phase C: Implementation, Testing & PR**
- [x] **Final: Pull Request Created**
- **Current Status:** Final - Pull Request Creation Completed

---

### 4. Final Gate
- Use the `AskUserQuestion` tool with the question "Would you like to switch back to the main branch?" and two options: label "Yes" and label "No".
- If Yes: run `git checkout main`.
- If No: await further instructions.
