# Sprint Change Proposal — Story 2.0 Planning Alignment

Date: 2026-03-01  
Workflow: Correct Course (`bmad-bmm-correct-course`)  
Trigger Story: `2-0-int64-coordinate-system-and-scale`

## 1) Issue Summary

Story `2-0-int64-coordinate-system-and-scale` exists and is tracked in implementation artifacts (`ready-for-dev`), but it is missing from the planning artifact `epics.md`.

This creates a planning/implementation mismatch:
- Sprint tracking includes Story 2.0.
- Epic 2 planning narrative starts at Story 2.1.
- Team members reading only `epics.md` do not see the prerequisite coordinate-system migration.

## 2) Impact Analysis

### Epic Impact
- **Epic 2 affected:** Story ordering and dependency visibility are incomplete in planning documentation.
- Story 2.1 depends on coordinate/scale conventions established by Story 2.0.

### Story Impact
- `2-0-int64-coordinate-system-and-scale` should be represented in Epic 2 as a technical enabler story.
- Existing Story 2.1–2.8 IDs remain unchanged.

### Artifact Conflicts
- **Planning artifact conflict:** `_bmad-output/planning-artifacts/epics.md` omits Story 2.0.
- **Implementation artifact consistency:** `_bmad-output/implementation-artifacts/sprint-status.yaml` already includes Story 2.0, no structural conflict.

### Technical Impact
- No runtime/code change required.
- Documentation alignment only.

## 3) Recommended Approach

### Selected Path: Direct Adjustment (Option 1)

Add Story 2.0 to Epic 2 in `epics.md` as a prerequisite technical foundation story.

Rationale:
- Lowest risk, minimal surface area.
- Preserves existing sprint status and story numbering.
- Removes ambiguity for dev-story and review workflows.

Effort: Low  
Risk: Low  
Timeline Impact: None (documentation-only correction)

## 4) Detailed Change Proposals

### Artifact: `_bmad-output/planning-artifacts/epics.md`

#### Change A — Insert Story 2.0 section before Story 2.1

**OLD**
- Epic 2 starts with `### Story 2.1: Living Belt — Asteroid Physics Simulation`

**NEW**
- Insert `### Story 2.0: int64 Coordinate System and Scale Migration` above Story 2.1, including:
  - Developer-focused user story statement
  - Acceptance criteria for:
    - int64 entity coordinates (`X`, `Y`)
    - sector grid fields (`GridX`, `GridY`, `IsGenerated`)
    - canonical `RegionBounds` constants
    - physics scale constants and `(long)` integration cast
    - mm-scale generator/spawn contracts and snapshot types
    - build/test expectations

Justification:
- Aligns planning with existing implementation artifact.
- Explicitly documents prerequisite model constraints for all Epic 2+ stories.

### Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

#### Change B — No change required

**Status**
- Story 2.0 already present and correctly marked `ready-for-dev`.

Justification:
- Correct Course action is planning artifact reconciliation, not sprint-status restructuring.

## 5) Implementation Handoff

Scope Classification: **Minor**

Handoff Recipients:
- Primary: Scrum Master / planning artifact maintainer
- Secondary: PM (optional review for narrative consistency)

Responsibilities:
- Apply `epics.md` insertion for Story 2.0.
- Confirm story ordering remains 2.0 → 2.1 ... 2.8.
- Keep `sprint-status.yaml` unchanged.

Success Criteria:
- Story 2.0 appears in Epic 2 planning section.
- Planning and implementation artifacts are consistent.
- No story IDs are renumbered.

## Checklist Outcome Summary

- Trigger/context identification: ✅ Done
- Epic impact assessment: ✅ Done
- Artifact conflict analysis: ✅ Done
- Path forward evaluation: ✅ Done (Option 1 selected)
- Proposal and handoff definition: ✅ Done
- Sprint status update requirement: N/A (no structural changes needed)
