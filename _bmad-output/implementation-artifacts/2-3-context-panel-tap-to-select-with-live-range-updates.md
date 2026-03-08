# Story 2.3: Context Panel — Tap-to-Select with Live Range Updates

Status: review

## Story

As a **player**,
I want to tap any object in the game world and see a context panel showing available actions that update live as I fly closer,
so that I always know what I can do with any object at my current distance — without ever needing to re-tap.

## Acceptance Criteria

1. **Given** the player taps/clicks any interactive object on the canvas,
   **when** the tap registers,
   **then** the context panel slides in from the right edge with the object's name, type icon, distance indicator, and distance-appropriate actions.

2. **Given** the player taps/clicks any interactive object on the canvas,
   **when** that object becomes selected,
   **then** the selected object is visually highlighted in-world until the panel is dismissed or a different object is selected.

3. **Given** the player is at far range (object visible but out of scan range),
   **when** the panel is open,
   **then** only `[Set Course]` and object info are shown; a "Get closer for more" footer is visible.

4. **Given** the player flies to scan range without re-tapping,
   **when** the scan range threshold is crossed,
   **then** `[Scan]` appears in the panel via fade-in transition; no re-tap required.

5. **Given** the player flies to mining range,
   **when** the mining range threshold is crossed,
   **then** `[Mine]`, `[Drill]`, and/or `[Claim]` appear (context-dependent); the "Get closer for more" footer disappears.

6. **Given** the player moves back out of range mid-interaction,
   **when** a range gate is lost,
   **then** the corresponding action greys out (not disappears) in the panel.

7. **Given** the panel is open,
   **when** the player explicitly taps the panel close `X` button,
   **then** the panel dismisses and the selected-object highlight is cleared.

8. **Given** the panel on keyboard navigation,
   **when** open,
   **then** focus is trapped within the panel; Escape dismisses it (`role="complementary"`, `aria-label="[object name] actions"`).

9. **Given** all panel touch targets,
   **then** each action button is at least 44×44px.

## Tasks / Subtasks

- [x] Task 1 — Add selection intent pipeline from canvas to UI state (AC: 1, 2, 7)
  - [x] Extend world interaction handling so asteroid/object taps are captured in the Pixi canvas layer and converted to a selected target object.
  - [x] Add a lightweight selection state module (selected object ID/type + last-known distance + available actions) that can be updated every world tick without reactive framework dependencies.
  - [x] Add selected-object visual highlight state and rendering hook so highlight appears on select and clears on deselect/reselect.
  - [x] Ensure tapping the same selected object keeps it selected and does not close the panel.
  - [x] Ensure tapping a different object transfers selection and triggers panel content crossfade.

- [x] Task 2 — Implement contextual panel component scaffold and lifecycle (AC: 1, 6, 7, 8)
  - [x] Replace the current `client/src/ui/ContextualPanel.ts` stub with a mounted HTML overlay component that opens from the right edge.
  - [x] Mount/unmount panel lifecycle from `client/src/app.ts` (or equivalent orchestrator) without breaking existing HUD bottom bar lifecycle.
  - [x] Implement semantic structure and accessibility baseline: `role="complementary"`, contextual `aria-label`, keyboard-focusable actions, Escape-to-dismiss.
  - [x] Add a persistent, touch-safe close `X` button in the panel header (top-right) that is always visible.
  - [x] Ensure action hit areas are at least 44×44px and pointer/touch behavior is equivalent.

- [x] Task 3 — Implement distance-gated action model and live updates (AC: 2, 3, 4, 5)
  - [x] Define deterministic range thresholds for far/scan/mining interaction states (as constants, not magic numbers).
  - [x] Recompute action availability continuously from authoritative ship/object positions in `WorldState` updates.
  - [x] Render always-available tier (`[Set Course]` + object info) and progressive action tiers (`[Scan]`, `[Mine]`, `[Drill]`, `[Claim]`) as range gates are crossed.
  - [x] Enforce degrade behavior when moving out of range: action becomes disabled/greyed, not removed.

- [x] Task 4 — Add transitions and panel behavior fidelity (AC: 1, 3, 6)
  - [x] Implement right-edge slide-in/out panel transition that respects reduced-motion preference.
  - [x] Add fade-in transition for newly unlocked actions (especially `[Scan]`) without forcing re-render churn.
  - [x] Add crossfade transition for panel content when selection transfers to another object.
  - [x] Keep selected object persisted while player moves; do not require re-tap on threshold crossings.

- [x] Task 5 — Integrate object metadata and context rendering (AC: 1, 2)
  - [x] Surface object name, icon/type token, and live distance indicator in panel header/body.
  - [x] Show "Get closer for more" helper when only far-range actions are available.
  - [x] Remove helper copy when mining-range options become available.

- [x] Task 6 — Accessibility, keyboard, and interaction safety checks (AC: 7, 8, 9)
  - [x] Add focus-trap behavior while panel is open and restore focus target when dismissed.
  - [x] Add Escape key handler with deterministic teardown.
  - [x] Verify close behavior is explicit (`X` and Escape) and no gesture-based dismiss is required.
  - [x] Validate minimum touch target dimensions in tests.

- [x] Task 7 — Testing and validation gates
  - [x] Add client unit tests for selection → panel open flow, distance threshold transitions, grey-out behavior, and dismiss interactions.
  - [x] Add tests for keyboard accessibility (focus trap, Escape close, role/label semantics).
  - [x] Add tests for reduced-motion behavior (no motion-heavy transitions).
  - [x] Run `npm run test` and `npm run build` in `client/`.

## Dev Notes

### Story Foundation (Epic 2)

- This story establishes the primary interaction model for Epic 2 and is the UX foundation for Stories 2.4+ (scan/mine actions are introduced through this panel).
- The panel must support intent-first interaction (tap target first), persistent selected-object visual highlighting, and distance-gated action reveal without re-selection.
- Scope is UI interaction architecture + client-side distance gating only; server-side scan/mining execution remains in follow-on stories.

### Architecture Compliance

- Respect two-layer UI split:
  - Layer 1 (PixiJS): object rendering and hit detection.
  - Layer 2 (HTML overlay): context panel UI and accessible controls.
- Keep state as plain TypeScript modules; do not introduce reactive frameworks.
- Keep JSON/state conventions `camelCase` on client and maintain existing SignalR normalization path in `WorldState`.

### Technical Requirements (Must Follow)

- Keep server-authoritative positional model:
  - Determine range gates from authoritative world snapshot data in `WorldState`.
  - Do not create speculative local object positions for gating logic.
- Preserve int64/mm coordinate assumptions from Story 2.0:
  - Use existing coordinate units and conversion utilities where distance display requires formatting.
- Maintain deterministic action gating:
  - Centralize thresholds and action-resolution rules in one place for testability.
- Do not block existing gameplay controls:
  - Panel interaction should not interfere with flight input outside panel bounds.

### Library / Framework Requirements

- Client stack baseline (repository pinned):
  - `pixi.js` `^8.16.0`
  - `@microsoft/signalr` `^10.0.0`
  - `@microsoft/signalr-protocol-msgpack` `^10.0.0`
  - `tailwindcss` `^4.2.0`
  - `typescript` `~5.9.3`
- Respect `prefers-reduced-motion` and existing accessibility patterns used in HUD/notification overlays.
- Keep strict TypeScript compatibility (`noUnusedLocals`, `noUnusedParameters`).

### File Structure Requirements

- Expected touchpoints:
  - `client/src/ui/ContextualPanel.ts`
  - `client/src/app.ts`
  - `client/src/state/WorldState.ts` (selection and/or distance-driven panel update wiring)
  - `client/src/rendering/layers/WorldLayer.ts` (interactive object selection integration)
  - `client/src/rendering/Renderer.ts` (if coordination methods are needed)
  - `client/src/style.css` (panel styles if required by current styling approach)
- Keep Story 2.2 HUD (`HudBottomBar`) integration intact and non-conflicting.

### Testing Requirements

- Add client tests for:
  - Tap/click target selection opens panel with object metadata.
  - Tap same selected object keeps panel open and selection unchanged.
  - Tap a different object transfers selection and crossfades panel content.
  - Selected object gets an in-world highlight on selection and highlight is cleared on dismiss/reselect.
  - Range threshold crossings unlock actions live without re-tap.
  - Out-of-range regression greys out actions instead of removing them.
  - Dismiss behavior: close `X` and Escape.
  - Accessibility semantics and focus management.
  - Minimum target dimensions and reduced-motion behavior.

## Engineering Checklist (Dev-Ready)

- [ ] `WorldLayer` emits object selection events with target identity (`objectType`, `objectId`) and world coordinates.
- [ ] Selection state module stores: selected target, current distance, and current action availability tier.
- [ ] Selected object highlight renders immediately and persists until explicit close or target transfer.
- [ ] Re-tap same target is idempotent (no panel close, no selection reset).
- [ ] Transfer to a different target updates highlight and crossfades panel content.
- [ ] Context panel header includes object name/type/distance and persistent close `X` button.
- [ ] Panel close via `X` clears selection + highlight and untraps focus.
- [ ] Escape closes panel and mirrors `X` close behavior exactly.
- [ ] Far/scan/mining range thresholds are centralized constants (no magic numbers).
- [ ] Action rows remain visible but disabled/greyed when out of range.
- [ ] Reduced-motion mode disables or simplifies slide/crossfade transitions.
- [ ] Tests cover selection, transfer, close (`X`/Escape), highlighting, range gating, and reduced-motion paths.

### Previous Story Intelligence

- Story 2.2 (`HUD Bottom Bar`) established robust overlay lifecycle and accessibility/pulse patterns; reuse mounting and reduced-motion approaches rather than inventing a new pattern.
- Story 2.2a (`Sparse-space motion reference`) confirmed render-layer boundaries and non-interactive background discipline; preserve interaction ownership in world/panel layers only.
- Current code reality: `client/src/ui/ContextualPanel.ts` is currently a stub (`export class ContextualPanel {}`), so this story includes first real implementation of panel behavior.

### Git Intelligence Summary

- Recent commit history could not be reliably retrieved from this execution context terminal output.
- Use the current mainline implementation patterns from Stories 2.2 and 2.2a as the immediate baseline for consistency.

### Latest Technical Information

- No external web lookup was performed in this workflow run.
- Use repository-pinned dependency versions above as implementation source of truth unless a separate dependency-upgrade story explicitly changes them.

### Project Structure Notes

- Story maps directly to FR35 client side in architecture mapping (`ui/ContextualPanel.ts`, `rendering/layers/UiLayer.ts`).
- Selection signal originates from canvas interactions, but contextual panel rendering and accessibility requirements are HTML overlay concerns.

### References

- `_bmad-output/planning-artifacts/epics.md` (Story 2.3 definition and acceptance criteria)
- `_bmad-output/planning-artifacts/ux-design-specification.md` (defining interaction model, distance-gated actions, implementation approach)
- `_bmad-output/planning-artifacts/architecture.md` (frontend architecture and file mapping)
- `project-context.md` (stack conventions, naming rules, coordinate system constraints)
- `_bmad-output/implementation-artifacts/2-2-hud-bottom-bar-pulse-indicator.md` (overlay/accessibility patterns)
- `_bmad-output/implementation-artifacts/2-2a-sparse-space-motion-reference-background.md` (render-layer boundary learnings)

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- Story context generated via BMAD create-story workflow against sprint backlog ordering.
- Auto-selected next backlog story key: `2-3-context-panel-tap-to-select-with-live-range-updates`.
- Story document assembled from epics, architecture, UX specification, project context, and previous Epic 2 story artifacts.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story prepared for implementation with explicit guardrails against interaction regressions, accessibility misses, and architecture drift.
- Implemented asteroid selection pipeline from Pixi world interactions through `ObjectSelectionState` to HTML contextual panel updates.
- Replaced `ContextualPanel` stub with accessible right-side overlay: explicit close `X`, `Escape` handling, focus trap, contextual aria labeling, and touch-safe action targets.
- Added selected-asteroid in-world highlight rendering and persisted selection behavior across movement and range-threshold crossings.
- Implemented deterministic far/scan/mining action gating with progressive action reveal and out-of-range grey-out behavior.
- Added action unlock/crossfade transitions with reduced-motion fallbacks.
- Added client unit tests for selection-state distance gating and contextual panel accessibility/interaction behavior.
- Validated client quality gates: full `npm run test` suite pass and `npm run build` success.

### File List

- _bmad-output/implementation-artifacts/2-3-context-panel-tap-to-select-with-live-range-updates.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- client/src/app.ts
- client/src/style.css
- client/src/rendering/Renderer.ts
- client/src/rendering/entities/AsteroidRenderer.ts
- client/src/rendering/layers/WorldLayer.ts
- client/src/state/ObjectSelectionState.ts
- client/src/state/ObjectSelectionState.test.ts
- client/src/ui/ContextualPanel.ts
- client/src/ui/ContextualPanel.test.ts

## Change Log

- 2026-03-02: Story created via BMAD create-story workflow; status set to `ready-for-dev`.
- 2026-03-02: Implemented contextual panel selection, highlighting, live distance-gated actions, accessibility behavior, and client tests; status moved to `review`.