# Sprint Change Proposal — Sparse-Space Motion Reference Background

Date: 2026-03-01  
Workflow: Correct Course (`bmad-bmm-correct-course`)  
Trigger Area: In-flight readability during sparse asteroid visibility

## 1) Issue Summary

### Problem Statement
When there are no asteroids visible on-screen, players lack a stable visual reference and cannot reliably infer ship travel direction or drift.

### Discovery Context
- Triggered from active play feedback during Epic 2 implementation.
- Current client render stack includes an empty BackgroundLayer and therefore no low-contrast motion cue.
- HUD location context exists but does not provide immediate directional feel while flying moment-to-moment.

### Evidence
- Player-reported friction: “extremely hard… to tell which direction their ship is travelling in” when screen is empty.
- Existing background implementation is effectively blank in `BackgroundLayer`.
- Current UX/docs do not explicitly require sparse-space motion-reference visuals.

## 2) Impact Analysis

### Epic Impact
- **Epic 2 affected:** Add a small story-scoped UX/rendering enhancement for ambient orientation.
- No new epic required.
- No epic resequencing required.

### Story Impact
- **Directly impacted:** Add a new story after Story 2.2 (proposed `2.2a`) focused on in-flight motion reference background.
- **Indirectly impacted:** Story 2.2 remains unchanged functionally, but benefits from improved readability alongside the HUD bottom bar.

### Artifact Conflicts
- **Epics:** `epics.md` should include a dedicated story for sparse-space motion reference.
- **UX spec:** needs explicit guidance for faint background orientation cues and reduced-motion variant.
- **Architecture:** add implementation note to keep this in BackgroundLayer only.
- **PRD:** no conflict; this is consistent with FR35 ambient contextual readability.

### Technical Impact
- Client-only rendering work in `BackgroundLayer` with minimal integration risk.
- Candidate evaluation requires two variants:
  1. Faint starfield constellation
  2. Faint sector-aligned grid
- Must preserve readability of HUD/context UI and respect reduced-motion preference.
- No server, infra, schema, or API contract changes required.

## 3) Recommended Approach

### Selected Path: Direct Adjustment (Option 1)

Introduce a dedicated story (`2.2a`) that implements and evaluates both motion-reference candidates in `BackgroundLayer`, then selects one for MVP.

### Option Evaluation
- **Option 1 Direct Adjustment:** **Viable** (Effort: Low/Medium, Risk: Low)
- **Option 2 Potential Rollback:** Not viable (no prior implementation to revert)
- **Option 3 MVP Review:** Not viable (MVP goals unchanged; this is a clarity enhancement)

### Rationale
- Solves the exact gameplay friction with minimal surface area.
- Leverages existing architecture seam (`BackgroundLayer`) without adding UI complexity.
- Keeps solution reversible and testable while preserving current epic momentum.

## 4) Detailed Change Proposals

### A) Story Changes

#### Story: New `2.2a` (insert between 2.2 and 2.3)
Section: Story definition

**OLD:**
- No dedicated story for sparse-space directional readability when nearby objects are absent.

**NEW:**
As a **player**,  
I want a very faint background motion reference while flying in sparse space,  
so that I can perceive ship travel direction and drift even when no asteroids are visible.

**Proposed Acceptance Criteria:**
1. **Given** open-space flight with no nearby visible asteroids, **when** rendering updates, **then** a faint non-intrusive motion-reference layer remains visible in the background.
2. **Given** evaluation mode, **when** the candidate treatments are compared, **then** both a faint starfield and a faint sector-aligned grid are available for side-by-side review.
3. **Given** final MVP selection, **when** evaluation completes, **then** one treatment is selected as default and the non-selected treatment is removed from MVP scope.
4. **Given** HUD/context overlays are present, **when** the motion-reference layer renders, **then** HUD readability and interaction clarity are not degraded.
5. **Given** reduced-motion preference, **when** enabled, **then** the motion-reference layer renders static or minimally animated.

**Rationale:** Captures user-reported directional readability pain as a focused, testable story.

---

### B) UX Specification Changes

#### Artifact: `_bmad-output/planning-artifacts/ux-design-specification.md`
Section: Visual/ambient flight guidance

**OLD:**
- No explicit requirement for sparse-space background directional cues.

**NEW:**
- Add a sparse-space orientation guideline:
  - Render a very subtle motion-reference background during empty-field flight.
  - Evaluate starfield and sector-aligned grid patterns; choose one for MVP.
  - Keep contrast intentionally low so gameplay objects and HUD remain dominant.
  - Provide reduced-motion behavior (static/minimal animation).

**Rationale:** Prevents regressions and makes directional readability a documented UX contract.

---

### C) Architecture / Implementation Notes

#### Artifact: `_bmad-output/planning-artifacts/architecture.md` + implementation story notes

**OLD:**
- No explicit architectural note for motion-reference background ownership.

**NEW:**
- Add implementation note: motion-reference rendering belongs exclusively to `BackgroundLayer` in client renderer layering.
- Keep world entities in `WorldLayer` and HUD/context overlays in HTML overlay layer.
- Do not introduce gameplay logic coupling to this visual aid.

**Rationale:** Preserves clean boundaries and minimizes implementation risk.

### D) PRD Changes

No PRD modifications required.

## 5) Implementation Handoff

### Scope Classification
**Minor**

### Handoff Recipients and Responsibilities
- **Development team:** implement `2.2a` in client renderer (`BackgroundLayer`) with two candidate treatments and final selection.
- **PO/SM:** add new story entry and ordering in `epics.md`, align sprint sequencing.
- **UX maintainer:** add sparse-space orientation guideline to UX specification.

### Success Criteria
- Players can perceive travel direction during empty-field flight.
- Selected visual aid remains faint and non-distracting.
- No HUD/context readability regression.
- Reduced-motion behavior is preserved.

## Checklist Outcome (Interactive Execution)

### Section 1 — Trigger and Context
- 1.1 Trigger story/area identified: **[x] Done** (sparse-space directional readability)
- 1.2 Core problem defined: **[x] Done**
- 1.3 Evidence gathered: **[x] Done**

### Section 2 — Epic Impact Assessment
- 2.1 Current epic viability: **[x] Done** (Epic 2 remains viable)
- 2.2 Epic-level changes: **[x] Done** (add focused story 2.2a)
- 2.3 Remaining epics review: **[N/A] Skip** (no downstream invalidation)
- 2.4 Future epic invalidation check: **[N/A] Skip**
- 2.5 Epic order/priority check: **[x] Done** (story insertion after 2.2)

### Section 3 — Artifact Conflict Analysis
- 3.1 PRD conflict check: **[x] Done** (none)
- 3.2 Architecture conflict check: **[x] Done** (boundary note required only)
- 3.3 UI/UX conflict check: **[x] Done** (UX spec update needed)
- 3.4 Other artifacts check: **[x] Done** (epics + implementation docs)

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
- 6.4 `sprint-status.yaml` update: **[x] Done** (added `2-2a-sparse-space-motion-reference-background: backlog`)
- 6.5 Next steps confirmation: **[x] Done**

## Approval and Routing Record

- Approval Status: **Approved**
- Approved By: Richard
- Approval Date: 2026-03-01
- Scope Classification: **Minor**
- Route To: **Development team (direct implementation)**

### Routing Deliverables

1. Added Story 2.2a in epics planning with acceptance criteria for sparse-space motion reference evaluation and selection.
2. Added sprint tracker entry `2-2a-sparse-space-motion-reference-background: backlog`.
3. Added UX guidance for sparse-space motion-reference behavior.
4. Added architecture boundary note constraining implementation to `BackgroundLayer`.

### Implementation Success Criteria

- Empty-field flight provides a faint directional cue.
- Candidate comparison (starfield vs sector-aligned grid) is performed and one is selected for MVP.
- HUD/context readability remains unaffected.
- Reduced-motion behavior is respected.
