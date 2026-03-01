# Sprint Change Proposal — Story 2.2a Scope Alignment and Selection Finalization

Date: 2026-03-01
Workflow: Correct Course (`bmad-bmm-correct-course`)
Mode: Batch (assumed; can switch to Incremental on request)
Trigger Story: `2-2a-sparse-space-motion-reference-background`

## 1) Issue Summary

### Problem Statement
Story `2.2a` implementation evolved through rapid visual tuning and now has a documentation/scope alignment gap: the runtime default treatment was switched to `grid`, both `grid` and `starfield` remain selectable in development/runtime paths, and story notes still state `starfield` as MVP default.

### Discovery Context
- Discovered during iterative live visual QA after initial implementation of Story 2.2a.
- Multiple user-directed adjustments were applied to improve visibility/readability and world-orientation feel.
- Current code behavior is stable and validated, but planning/implementation artifacts are partially stale.

### Evidence
- `client/src/rendering/layers/BackgroundLayer.ts` resolves default treatment to `grid` and supports `bgRef=grid|starfield|stars`.
- `_bmad-output/implementation-artifacts/2-2a-sparse-space-motion-reference-background.md` still records default MVP treatment as `starfield` in implementation notes.
- Story acceptance criterion 3 says the non-selected treatment should be removed from MVP scope, while the implementation keeps both selectable.

## 2) Impact Analysis

### Epic Impact
- **Epic 2 remains viable** and in progress.
- No new epic required.
- No epic resequencing required.

### Story Impact
- **Directly impacted:** Story `2.2a` artifact text and acceptance-scope interpretation.
- **Adjacent impacted story:** none functionally; this is primarily artifact/spec alignment.

### Artifact Conflict Analysis
- **PRD:** No conflict with core goals; change is still within FR35 UX readability intent.
- **Architecture:** No component-model conflict; implementation remains correctly isolated in `BackgroundLayer`.
- **UX specification:** Requires explicit final-selection statement so treatment default and retained toggle behavior are unambiguous.
- **Implementation artifact (story file):** Requires correction of outdated “default starfield” notes.
- **Sprint tracker:** Status already `review`; no status transition required from this change alone.

### Technical Impact
- Minimal code impact expected (current implementation already stable).
- Potentially documentation-only update unless strict AC interpretation enforces code removal of non-selected runtime path.
- No server, API, database, infra, or deployment impacts.

## 3) Recommended Approach

### Selected Path
**Option 1 — Direct Adjustment (documentation + acceptance clarification, no rollback).**

### Option Evaluation
- **Option 1: Direct Adjustment** — **Viable** (Effort: Low, Risk: Low)
- **Option 2: Potential Rollback** — Not viable (would discard validated tuning)
- **Option 3: MVP Review** — Not needed (MVP remains intact)

### Rationale
- Preserves working, validated visuals and player-facing improvements.
- Removes ambiguity between implementation and story artifact.
- Avoids unnecessary rework unless product intent is to strictly remove alternate treatment at runtime.

## 4) Detailed Change Proposals

### A) Story Artifact Update (`2-2a-sparse-space-motion-reference-background.md`)

**Proposal A1 — Correct default treatment statement**

Story: `2-2a-sparse-space-motion-reference-background`
Section: `Dev Agent Record > Implementation Plan` and `Completion Notes List`

OLD:
- “Keep runtime default to starfield for MVP...”
- “default MVP runtime treatment is `starfield` ...”

NEW:
- “Set runtime default treatment to `grid` after comparative evaluation and visual QA.”
- “`starfield` remains an alternate selectable treatment via `bgRef` for evaluation/debug, unless later removed by explicit scope decision.”

Rationale:
- Aligns story artifact with actual runtime behavior.

**Proposal A2 — Clarify AC3 interpretation (selected vs removed treatment)**

Section: `Acceptance Criteria #3` interpretation note in Dev Notes

OLD:
- Non-selected treatment is removed from MVP scope.

NEW:
- Selected treatment (`grid`) is the MVP default.
- Non-selected treatment (`starfield`) is out of the primary MVP path but may remain behind explicit dev/query override for evaluation and troubleshooting.

Rationale:
- Preserves practical debug capability while maintaining clear default behavior.

### B) UX Spec Addendum (`ux-design-specification.md`)

Section: sparse-space orientation treatment decision note

OLD:
- Mentions evaluation of starfield vs sector-aligned grid but no final selected default declaration.

NEW:
- Declare final selected default for current sprint: **sector-aligned grid**.
- Keep alternate starfield as non-default evaluation mode only.

Rationale:
- Removes ambiguity for future stories and prevents accidental regressions.

### C) Epics/Planning Alignment (`epics.md`)

Section: Story `2.2a` implementation note

OLD:
- Story requires final selection and removal language without implementation nuance.

NEW:
- Add planning note: “Selected treatment for MVP default is `grid`; alternate treatment may remain only as explicit non-default evaluation path.”

Rationale:
- Synchronizes planning intent with current engineering reality.

### D) Code Change Scope

No immediate code changes required to satisfy this proposal.
If product intent requires strict removal of non-selected treatment at runtime, follow-up story/task should remove query-based alternate selection.

## 5) Implementation Handoff

### Scope Classification
**Moderate** (artifact and backlog coordination across multiple planning docs; no deep architecture replan)

### Handoff Recipients
- **Product Owner / Scrum Master**: approve AC interpretation and update planning artifacts.
- **Development team**: apply any follow-up code changes only if strict non-selected-treatment removal is mandated.
- **UX maintainer**: record finalized treatment decision in UX spec.

### Responsibilities
- PO/SM: confirm whether alternate treatment can remain behind explicit override.
- Dev: keep runtime default behavior consistent with approved decision.
- UX: maintain one source of truth for visual treatment selection.

### Success Criteria
- Story 2.2a artifact and planning docs match actual behavior.
- Final treatment decision is explicit (`grid` default).
- No ambiguity remains on whether alternate treatment is allowed in non-default path.

---

## Checklist Execution Record

### Section 1 — Understand Trigger and Context
- 1.1 Triggering story identified: **[x] Done** (`2.2a`)
- 1.2 Core problem defined: **[x] Done** (scope/docs drift vs implementation)
- 1.3 Evidence gathered: **[x] Done** (code + artifact mismatch)

### Section 2 — Epic Impact Assessment
- 2.1 Current epic viability: **[x] Done** (viable)
- 2.2 Epic-level changes needed: **[x] Done** (none structural)
- 2.3 Remaining epics impact review: **[x] Done** (no downstream blockers)
- 2.4 Future epic invalidation/new epic need: **[N/A] Skip**
- 2.5 Epic priority/order change: **[N/A] Skip**

### Section 3 — Artifact Conflict and Impact
- 3.1 PRD conflict check: **[x] Done** (none)
- 3.2 Architecture conflict check: **[x] Done** (none)
- 3.3 UI/UX conflict check: **[x] Done** (decision statement needed)
- 3.4 Other artifacts impact: **[x] Done** (story + planning docs)

### Section 4 — Path Forward Evaluation
- 4.1 Direct adjustment: **[x] Viable**
- 4.2 Potential rollback: **[ ] Not viable**
- 4.3 PRD MVP review: **[ ] Not viable**
- 4.4 Selected approach/rationale: **[x] Done** (Option 1)

### Section 5 — Proposal Components
- 5.1 Issue summary: **[x] Done**
- 5.2 Epic/artifact adjustment summary: **[x] Done**
- 5.3 Recommended path + rationale: **[x] Done**
- 5.4 MVP impact + action plan: **[x] Done**
- 5.5 Agent handoff plan: **[x] Done**

### Section 6 — Final Review and Handoff
- 6.1 Checklist completeness review: **[x] Done**
- 6.2 Proposal accuracy review: **[x] Done**
- 6.3 Explicit user approval obtained: **[x] Done** (Approved: yes)
- 6.4 `sprint-status.yaml` updated (if epic/story map changes approved): **[N/A] Skip**
- 6.5 Next steps/timeline confirmed with user: **[x] Done**

---

## Approval and Routing Record

Approval Status: **Approved**
Approved By: Richard
Approval Date: 2026-03-01

### Scope Classification
**Moderate**

### Routing Decision
- **Primary route:** Product Owner / Scrum Master for artifact alignment and backlog-note updates.
- **Secondary route:** UX maintainer for explicit treatment-selection decision note.
- **Conditional route:** Development team only if strict removal of non-selected treatment from runtime path is requested.

### Confirmed Next Steps
1. Update Story 2.2a implementation artifact notes to reflect `grid` default.
2. Add explicit selected-treatment note in UX planning docs.
3. Add planning note in epics context clarifying non-default treatment policy.
4. Keep `sprint-status.yaml` unchanged unless additional implementation tasks are added.

### Workflow Execution Log
- Issue addressed: Story 2.2a scope/documentation alignment after visual tuning changes.
- Change scope: Moderate.
- Artifacts targeted: Story implementation artifact, UX design spec, epics planning notes.
- Routed to: PO/SM (primary), UX maintainer (secondary), Dev (conditional).
