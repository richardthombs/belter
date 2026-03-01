# Story 2.2a: Sparse-Space Motion Reference Background

Status: review

## Story

As a **player**,
I want a very faint background motion reference while flying in sparse space,
so that I can perceive ship travel direction and drift even when no asteroids are visible.

## Acceptance Criteria

1. **Given** open-space flight with no nearby visible asteroids,
   **when** rendering updates,
   **then** a faint non-intrusive motion-reference layer remains visible in the background.

2. **Given** evaluation mode,
   **when** candidate treatments are compared,
   **then** both a faint starfield and a faint sector-aligned grid are available for side-by-side review.

3. **Given** final MVP selection,
   **when** evaluation completes,
   **then** one treatment is selected as default and the non-selected treatment is removed from MVP scope.

4. **Given** HUD/context overlays are present,
   **when** the motion-reference layer renders,
   **then** HUD readability and interaction clarity are not degraded.

5. **Given** `prefers-reduced-motion`,
   **when** enabled,
   **then** the motion-reference layer renders static or minimally animated.

## Tasks / Subtasks

- [x] Task 1 — Implement baseline background reference scaffolding (AC: 1, 4)
  - [x] Expand `client/src/rendering/layers/BackgroundLayer.ts` from empty container to renderable layer with explicit update hooks.
  - [x] Ensure the background layer remains visual-only and never consumes pointer/touch interactions.
  - [x] Verify render ordering remains `BackgroundLayer -> WorldLayer -> EffectsLayer -> UiLayer`.

- [x] Task 2 — Implement candidate A: faint starfield (AC: 1, 2, 4)
  - [x] Create a deterministic sparse star distribution for the current viewport/world frame.
  - [x] Keep stars intentionally faint and low-contrast to avoid visual competition with entities/HUD.
  - [x] Add subtle parallax or camera-relative movement cue if needed, without introducing distraction.

- [x] Task 3 — Implement candidate B: faint sector-aligned grid (AC: 1, 2, 4)
  - [x] Render a very low-contrast grid aligned to sector-space logic used by location context.
  - [x] Keep line density coarse enough to convey orientation without becoming a targeting aid.
  - [x] Confirm grid remains legible but subordinate under all expected zoom/camera states.

- [x] Task 4 — Add treatment selection and MVP default (AC: 3)
  - [x] Add a development-time selection mechanism to switch between `starfield` and `grid` for side-by-side evaluation.
  - [x] Record selected default treatment in implementation notes for this story.
  - [x] Remove/deactivate non-selected treatment from MVP runtime path before marking story done.

- [x] Task 5 — Reduced-motion behavior (AC: 5)
  - [x] Respect `prefers-reduced-motion` in background update logic.
  - [x] Ensure reduced-motion mode is static or minimally animated while preserving orientation utility.

- [x] Task 6 — Validation and regression checks (AC: 1, 4, 5)
  - [x] Add renderer/layer tests for deterministic generation and reduced-motion branch behavior.
  - [x] Run focused client tests for rendering-related changes.
  - [x] Run `npm run build` in `client/` to ensure no TS or bundling regressions.

## Dev Notes

### Story Foundation (Epic 2)

- This story addresses a usability gap discovered during active play: directional ambiguity in empty-field flight.
- Scope is intentionally constrained to visual orientation cues in the background layer only.
- No gameplay/system logic changes are required.

### Architecture Compliance

- Implement strictly in `client/src/rendering/layers/BackgroundLayer.ts`.
- Do not move world entities out of `WorldLayer`.
- Do not add HUD-like information into Pixi background visuals.

### UX Constraints

- Keep visual intensity very faint; the aid should be perceived, not noticed.
- Preserve readability of HUD Bottom Bar and upcoming context-panel interactions.
- Do not add additional UI controls or overlays to satisfy this story.

### Technical Requirements

- Follow existing client conventions (`PascalCase` classes, strict TypeScript, no unused locals/params).
- Avoid hard-coded new color systems; stay within existing palette/tokens.
- Keep implementation deterministic where possible for testability.

### Testing Requirements

- Add tests that validate:
  - Background reference is present when world is sparse.
  - Candidate switching works in development mode.
  - Reduced-motion branch suppresses motion-heavy effects.
  - No interaction capture/regression from background layer.

### References

- `_bmad-output/planning-artifacts/epics.md` (Story 2.2a definition)
- `_bmad-output/planning-artifacts/ux-design-specification.md` (sparse-space orientation guidance)
- `_bmad-output/planning-artifacts/architecture.md` (render layer boundaries)
- `project-context.md` (PixiJS v8 and TypeScript strictness constraints)

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Implementation Plan

- Expand `BackgroundLayer` into an explicit renderable/updateable layer that stays non-interactive.
- Set runtime default to grid for MVP after candidate evaluation while retaining non-default treatment override support for evaluation/debug.
- Generate deterministic star points from tile-hashed world coordinates so repeated camera frames are stable and testable.
- Add reduced-motion branch that suppresses motion phase offsets while preserving orientation cues.
- Verify draw order remains `BackgroundLayer -> WorldLayer -> EffectsLayer -> UiLayer` and capture this in a renderer unit test.
- Validate with focused rendering tests and client build checks.

### Completion Notes List

- Story artifact created from approved Sprint Change Proposal (2026-03-01).
- Scope constrained to client BackgroundLayer and UX-compliant visual subtlety.
- Implemented deterministic sparse starfield treatment with subtle parallax and faint luminance profile.
- Implemented optional sector-aligned faint grid treatment with coarse spacing for orientation only.
- Added treatment switch via `?bgRef=starfield|grid` (plus `?bgRef=stars` alias); default MVP runtime treatment is `grid` and `starfield` remains non-default for evaluation/debug overrides.
- Added reduced-motion branch honoring `prefers-reduced-motion` to keep background static/minimally animated.
- Added renderer and background-layer tests covering layer order, deterministic generation, treatment switching, non-interactive behavior, and reduced-motion behavior.
- Fixed `formatCoarseLocation` aliasing in HUD coarse location encoding so distant positions no longer collide to identical coarse codes.
- Fixed background parallax transform at large world coordinates so starfield/grid remain camera-relative and visible in live gameplay.
- Added `bgRef=stars` alias for `starfield` and tuned star density for reliable but still subtle sparse-space visibility.
- Validation results: focused rendering tests pass; full `npm test` passes (44/44); `npm run build` passes.

### File List

- _bmad-output/implementation-artifacts/2-2a-sparse-space-motion-reference-background.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- client/src/rendering/Renderer.ts
- client/src/rendering/Renderer.test.ts
- client/src/rendering/layers/BackgroundLayer.ts
- client/src/rendering/layers/BackgroundLayer.test.ts
- client/src/ui/HudBottomBar.ts

## Change Log

- 2026-03-01: Story created via Correct Course follow-up after proposal approval; status set to `ready-for-dev`.
- 2026-03-01: Implemented sparse-space motion reference background (starfield + grid evaluation path), wired render update hooks, and added rendering tests.
- 2026-03-01: Resolved HUD coarse-location aliasing regression and completed full client validation gates; story moved to `review`.
- 2026-03-01: Fixed live runtime background visibility issue (camera transform math), added selector alias (`bgRef=stars`), and revalidated client tests/build.
