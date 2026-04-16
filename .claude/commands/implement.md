---
description: "Phase C implementation, testing, and PR output"
name: "Implement Feature"
argument-hint: "Feature Name (to locate Features/{FeatureName}/plan.md)"
agent: "agent"
---

# Phase C: Implementation, Testing & PR

**Context:** $ARGUMENTS

## 1. Status Initialization
- [x] **Phase A: Analysis**
- [x] **Phase B: Planning**
- [ ] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase C - Implementation Start

---

## 2. Instructions

0. **Pre-flight checks:** Run the following git checks before doing anything else. If any check fails, stop and tell the user what to fix — do not proceed with implementation.
   - `git branch --show-current` → must be `main`
   - `git status --porcelain` → must be empty **except** for files under `Features/$ARGUMENTS/` (untracked or modified feature docs are allowed)
   - `git cherry -v` → must be empty (no unpushed commits)

1. **Plan Retrieval:**
   - Read the technical blueprint from `Features/$ARGUMENTS/plan.md`.
   - Ensure you understand the architecture and signatures defined in Phase B.

2. **Execution:**
   - **Production Code:** Implement the logic in the existing codebase. Follow SOLID principles and project style guides.
   - **Verification:** Run the project's verification command as defined in CLAUDE.md. This project has no automated test projects — run `dotnet build ./BackEnd.csproj -c Release --no-incremental -warnaserror -v minimal` and confirm 0 errors, 0 warnings.

3. **PR Documentation:**
   - Generate a **Conventional Commit** message (e.g., `feat($ARGUMENTS): ...`).
   - Create a summary of changes, "How to Test" instructions, and a list of modified files.
   - Save this PR summary to `Features/$ARGUMENTS/pull_request.md`.

---

## 3. Final Status
- [x] **Phase A: Analysis**
- [x] **Phase B: Planning**
- [x] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase C - Implementation Completed

---

### 4. Final Gate
- Use the `AskUserQuestion` tool with the question "Implementation complete. Proceed to /pr?" and two options: label "Yes" and label "No".
- If Yes: invoke the `/pull_request $ARGUMENTS` skill.
- If No: await further instructions.
