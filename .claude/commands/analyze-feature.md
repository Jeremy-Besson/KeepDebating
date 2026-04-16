---
description: "Phase A analysis for a feature request"
name: "Analyze Feature"
argument-hint: "Feature request or goal"
agent: "agent"
---

## Phase A: Analysis

**Feature to analyze:** $ARGUMENTS

### 1. Status Initialization
- [ ] **Phase A: Analysis**
- [ ] **Phase B: Planning**
- [ ] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase A - Analysis Start

---

### 2. Analysis Task

Perform an initial analysis of the feature request:
* **Requirements:** What are the "must-haves"?
* **Constraints:** Technical or business limitations.
* **Edge Cases:** What could go wrong or be missed?
* **Dependencies:** External libraries, APIs, or internal modules.

**After analyzing:** Based on your analysis, use the `AskUserQuestion` tool to ask any questions that would help refine or confirm the requirements (up to 4 at once). Use the answers to update your analysis before saving the requirements file.

---

### 3. Generation & Storage
1.  **FeatureName:** Create a slug (max 30 chars, no spaces, e.g., `user-auth-revamp`).
2.  **FeatureDescription:** 1-2 concise lines.
3.  **RefinedRequirements:** A comprehensive Markdown list of the finalized scope.

**Action:** Use your file system tool to create the directory `Features/{FeatureName}` and save the `{RefinedRequirements}` into a file named `requirements.md` within that folder.

---

### 4. Output

#### Refined Requirements
**Name:** {FeatureName}  
**Description:** {FeatureDescription}  

{RefinedRequirements}

---

### 5. Final Status
- [x] **Phase A: Analysis**
- [ ] **Phase B: Planning**
- [ ] **Phase C: Implementation, Testing & PR**
- [ ] **Final: Pull Request Created**
- **Current Status:** Phase A - Analysis Completed

### 6. Final Gate
- Use the `AskUserQuestion` tool with the question "Proceed to /plan?" and two options: label "Yes" and label "No".
- If Yes: invoke the `/plan $ARGUMENTS` skill.
- If No: await further instructions.
