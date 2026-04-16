---
description: "Phase B technical planning from analysis"
name: "Plan Implementation"
argument-hint: "Feature Name (to locate Features/{FeatureName}/requirements.md)"
agent: "agent"
---

# Phase B: Planning

**Context:** $ARGUMENTS

## 1. Status Initialization
- [x] **Phase A: Analysis**
- [ ] **Phase B: Planning**
- [ ] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase B - Planning Start

---

## 2. Instructions

1. **Context Retrieval:**
   - Look for the requirements file at `Features/$ARGUMENTS/requirements.md`.
   - Read the file and use it as the source of truth for this plan.
   - If not present, say so and stop.

2. **Technical Blueprinting:**
   - Produce a detailed technical blueprint for integration into the existing codebase.
   - **Architecture:** Define where the logic lives (which folders/files).
   - **Signatures:** Provide class, interface, or function signatures.
   - **Data & Logic Flow:** Explain how data moves through the new feature.

3. **Risk Management:**
   - Call out risks, assumptions, and the validation/testing strategy.

4. **File Creation:**
   - Save this plan as `plan.md` inside the `Features/$ARGUMENTS/` directory.

---

## 3. Final Status
- [x] **Phase A: Analysis**
- [x] **Phase B: Planning**
- [ ] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase B - Planning Completed

### 4. Final Gate
- Use the `AskUserQuestion` tool with the question "Proceed to /implement?" and two options: label "Yes" and label "No".
- If Yes: invoke the `/implement $ARGUMENTS` skill.
- If No: await further instructions.
