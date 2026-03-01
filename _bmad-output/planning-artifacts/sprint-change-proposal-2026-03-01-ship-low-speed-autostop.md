# Sprint Change Proposal — Ship Low-Speed Auto-Stop Assist

Date: 2026-03-01
Workflow: Correct Course (`bmad-bmm-correct-course`)
Trigger Area: `PhysicsEngine` ship linear movement behavior

## 1) Issue Summary

### Problem Statement
Ships can remain drifting indefinitely at very low residual speeds, creating a poor usability experience when players expect to settle to stationary near the end of a maneuver.

### Discovery Context
- Observed during gameplay and tuning after Story 1.5 behavior stabilization.
- Existing behavior keeps strict Newtonian drift at all speeds when no thrust/brake is applied.

### Evidence
- Low-speed residual velocity persists without practical gameplay value.
- Requested behavior update: below 10 m/s, ship should gradually slow to fully stationary.

## 2) Impact Analysis

### Epic / Story Impact
- Affects Story `1-5-ship-flight-assisted-newtonian-physics` behavior semantics.
- No new epic required.
- No sprint resequencing required.

### Artifact Impact
- Implementation artifact for Story 1.5 requires acceptance criteria/dev-notes wording update.
- Planning epics/PRD/architecture do not require structural changes.

### Technical Impact
- Physics only: `PhysicsEngine.ApplyPhysics` low-speed path.
- Add/adjust unit tests around threshold behavior and stop snap.
- No API contract, database, migration, infra, or client protocol changes.

## 3) Recommended Approach

### Selected Path
Direct adjustment.

### Rationale
- Small, isolated change in ship linear damping behavior.
- Improves feel/controllability with low risk.
- Preserves Newtonian drift at regular speeds.

## 4) Detailed Change Proposals

### Physics behavior update
- Add low-speed assist threshold at `10_000 mm/s` (10 m/s).
- Apply damping only when:
  - `thrust == 0`
  - `brake == false`
  - `speed < 10_000 mm/s`
- Snap to zero velocity at tiny residual speed to avoid endless micro-drift.

### Test updates
- Keep test proving above-threshold no-input velocity remains unchanged.
- Add test proving below-threshold no-input velocity decays.
- Add test proving eventual snap-to-zero.

## 5) Implementation Handoff

### Scope Classification
Minor

### Handoff Recipients
- Development team (direct implementation)

### Success Criteria
- Ships above 10 m/s continue drifting unchanged with no-input/no-brake.
- Ships below 10 m/s gradually decelerate to zero.
- Physics test suite passes with threshold behavior explicitly covered.
