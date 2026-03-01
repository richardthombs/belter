# Sprint Change Proposal — Epic 2 HUD Location Context Refinement

Date: 2026-03-01  
Workflow: Correct Course (`bmad-bmm-correct-course`)  
Trigger Story: `2-2-hud-bottom-bar-pulse-indicator`

## 1) Issue Summary

### Problem Statement
During story clarification, a new requirement emerged for Story 2.2: the HUD should include **approximate location context** (sector number + coarse position inside sector), without meter-level precision.

### Discovery Context
- Triggered during implementation-readiness review of Story 2.2.
- Existing story only covers credits, hold %, and speed.
- UX intent supports ambient situational awareness, and HUD has an expandable zone for additional stat slots.

### Evidence
- Current Story 2.2 ACs omit location context entirely.
- UX spec states HUD purpose is ambient awareness and includes an expandable zone.
- Current world snapshots already include `sectorId` and ship `x`,`y` (mm), enabling derived coarse in-sector context with no precision overload.

## 2) Impact Analysis

### Epic Impact
- **Epic 2 affected:** Story 2.2 scope expands slightly within same epic.
- No new epic required.
- No epic resequencing required.

### Story Impact
- **Directly impacted:** `2-2-hud-bottom-bar-pulse-indicator` (acceptance criteria, tasks, dev notes).
- **Potentially impacted later:** Story 2.3 context panel may optionally reuse the same coarse-location formatter, but no mandatory change now.

### Artifact Conflicts
- **Epics:** Story 2.2 definition in `epics.md` needs to reflect the location-context requirement.
- **Implementation artifact:** Story 2.2 implementation guide needs AC/task updates.
- **UX spec:** HUD anatomy/behavior section should explicitly include location context slot behavior.
- **PRD:** No conflict; FR35 remains valid and this is a refinement inside existing UX intent.
- **Architecture:** No structural conflict; uses existing world model and contracts.

### Technical Impact
- Minor client/server contract extension likely needed to avoid brittle client-side sector math:
  - Preferred: include ship `sectorId` in ship snapshot payload.
  - Keep location display coarse via quantization (subsector label and rounded local km).
- No infra, deployment, or CI/CD changes required.

## 3) Recommended Approach

### Selected Path: Direct Adjustment (Option 1)

Refine Story 2.2 and supporting planning docs to include a **coarse location indicator** in the HUD:
- Sector number/id
- Subsector bucket (e.g., grid cell label)
- Rounded in-sector coordinates (coarse, non-meter precision)

### Option Evaluation
- **Option 1 Direct Adjustment:** **Viable** (Effort: Low/Medium, Risk: Low)
- **Option 2 Rollback:** Not viable (no implemented code to revert for this story)
- **Option 3 MVP Review:** Not viable (MVP unaffected; this is an in-scope refinement)

### Rationale
- Meets user requirement with minimal disruption.
- Aligns with HUD ambient-awareness goals.
- Keeps UI simple and non-noisy by design.

## 4) Detailed Change Proposals

### A) Story Changes

#### Story: `2-2-hud-bottom-bar-pulse-indicator`  
Section: Story statement

**OLD:**
As a **player**,  
I want a persistent bottom bar showing my credits, cargo hold percentage, and speed at all times during flight,  
so that I always have ambient situational awareness without any interaction required.

**NEW:**
As a **player**,  
I want a persistent bottom bar showing my credits, cargo hold percentage, speed, and coarse location context (sector + approximate in-sector position) at all times during flight,  
so that I always have ambient situational awareness without any interaction required.

**Rationale:** Adds explicit requirement coverage for location context.

---

#### Story: `2-2-hud-bottom-bar-pulse-indicator`  
Section: Acceptance Criteria (additions)

**OLD:**
- No AC for location context precision/format.

**NEW (add AC 7 and AC 8):**
7. **Given** the player is in flight,  
   **when** the HUD renders location context,  
   **then** it displays sector identifier plus coarse in-sector position (subsector-style bucket and rounded local coordinates), not meter-level precision.

8. **Given** location context values update during movement,  
   **when** the player crosses a coarse boundary,  
   **then** the location display updates without pulse animation and without `aria-live` announcements.

**Rationale:** Defines coarse-detail requirement and non-noisy behavior.

---

#### Story: `2-2-hud-bottom-bar-pulse-indicator`  
Section: Tasks/Subtasks (additions)

**OLD:**
- No implementation tasks for location context formatting or display.

**NEW (add under Task 5 and Task 6):**
- Add location formatter utility for coarse output:
  - Convert ship world position to local sector coordinates.
  - Quantize to coarse increments (recommended: rounded km) and subsector bucket (recommended: fixed grid label).
  - Clamp/format for negative and boundary transitions.
- Add HUD rendering for location slot (sector + subsector + coarse local coords).
- Add tests for quantization, boundary crossing, and stability (no high-frequency precision jitter).

**Rationale:** Makes implementation deterministic and testable.

---

### B) Epic/Planning Changes

#### Artifact: `_bmad-output/planning-artifacts/epics.md`  
Section: Epic 2, Story 2.2 ACs

**OLD:**
- Story 2.2 ACs end at reduced-motion behavior.

**NEW:**
- Append coarse location-context ACs equivalent to Story changes above.

**Rationale:** Keeps planning and implementation artifacts aligned.

---

### C) UX Specification Changes

#### Artifact: `_bmad-output/planning-artifacts/ux-design-specification.md`  
Section: HUD Bottom Bar anatomy/states

**OLD:**
- Anatomy: Credits, Hold, Speed, expandable zone.

**NEW:**
- Anatomy: Credits, Hold, Speed, **Location Context** (sector + subsector + coarse local coordinates).
- Behavior notes:
  - Coarse precision only; no meter-level detail.
  - Updates are quiet (no pulse, no live region announcements).
  - Designed for orientation, not exact targeting.

**Rationale:** Ensures UX contract explicitly covers your requested display semantics.

---

### D) Architecture / PRD Changes

#### Architecture
**OLD:** No explicit HUD location quantization note.

**NEW:** No architecture rewrite required; add implementation note in story only:
- Derive display from existing `sectorId` + ship position data.
- Keep quantization client-side and presentation-only.

#### PRD
No changes required.

## 5) Implementation Handoff

### Scope Classification
**Minor**

### Handoff Recipients and Responsibilities
- **Development team:** implement Story 2.2 refinements (HUD location slot + formatter + tests).
- **Scrum Master / PO:** apply planning artifact updates (`epics.md`, Story 2.2 artifact).
- **UX designer/maintainer:** update HUD anatomy/behavior language in UX spec.

### Success Criteria
- Story 2.2 explicitly includes coarse location requirement and AC coverage.
- HUD shows sector + subsector/coarse local coordinates without precision noise.
- No pulse/aria-live behavior added for location updates.
- Epics and UX docs remain consistent with implementation artifact.

## Checklist Outcome (Batch Execution)

### Section 1 — Trigger and Context
- 1.1 Trigger story identified: **[x] Done** (`2-2-hud-bottom-bar-pulse-indicator`)
- 1.2 Core problem defined: **[x] Done** (new requirement emerged)
- 1.3 Evidence gathered: **[x] Done**

### Section 2 — Epic Impact Assessment
- 2.1 Current epic viability: **[x] Done** (still viable)
- 2.2 Epic-level changes: **[x] Done** (modify existing story scope)
- 2.3 Remaining epics review: **[N/A] Skip** (no cross-epic dependency impact)
- 2.4 Future epic invalidation check: **[N/A] Skip**
- 2.5 Epic order/priority check: **[N/A] Skip**

### Section 3 — Artifact Conflict Analysis
- 3.1 PRD conflict check: **[x] Done** (no PRD conflict)
- 3.2 Architecture conflict check: **[x] Done** (no structural conflict)
- 3.3 UI/UX conflict check: **[x] Done** (HUD section update needed)
- 3.4 Other artifacts check: **[N/A] Skip**

### Section 4 — Path Forward Evaluation
- 4.1 Direct adjustment: **[x] Viable**
- 4.2 Rollback: **[ ] Not viable**
- 4.3 MVP review: **[ ] Not viable**
- 4.4 Selected approach and rationale: **[x] Done** (Option 1)

### Section 5 — Proposal Components
- 5.1 Issue summary: **[x] Done**
- 5.2 Epic/artifact adjustment summary: **[x] Done**
- 5.3 Recommended path with rationale: **[x] Done**
- 5.4 MVP impact and action plan: **[x] Done**
- 5.5 Handoff plan: **[x] Done**

### Section 6 — Final Review and Handoff Readiness
- 6.1 Checklist completion: **[x] Done**
- 6.2 Proposal accuracy verification: **[x] Done**
- 6.3 User approval: **[x] Done** (approved: yes)
- 6.4 `sprint-status.yaml` epic changes: **[N/A] Skip** (no epic/story list changes)
- 6.5 Next steps confirmation: **[x] Done**

## Approval and Routing Record

- Approval Status: **Approved**
- Approved By: Richard
- Approval Date: 2026-03-01
- Scope Classification: **Minor**
- Route To: **Development team (direct implementation)**
- Supporting Coordination: **PO/SM for artifact wording alignment**

### Routing Deliverables

1. Update Story 2.2 implementation artifact to include coarse location context ACs and tasks.
2. Update Epic 2 Story 2.2 entry in planning epics for wording parity.
3. Update UX HUD Bottom Bar section to include location-context anatomy and quiet-update behavior.
4. Preserve sprint structure/status entries (no sprint-status restructuring required).

### Implementation Success Criteria

- HUD shows sector identifier plus coarse in-sector location context (subsector-style and rounded coordinates).
- Location display avoids meter-level precision.
- Location updates are non-noisy (no pulse, no aria-live updates).
- Story, epics, and UX artifacts remain aligned.
