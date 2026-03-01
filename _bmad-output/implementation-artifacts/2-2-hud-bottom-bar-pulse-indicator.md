# Story 2.2: HUD Bottom Bar & Pulse Indicator

Status: ready-for-dev

## Story

As a **player**,
I want a persistent bottom bar showing my credits, cargo hold percentage, and speed at all times during flight,
so that I always have ambient situational awareness without any interaction required.

## Acceptance Criteria

1. **Given** the player is in flight,
   **when** the game canvas is visible,
   **then** the HUD bottom bar is persistently visible at the bottom edge of the viewport as a translucent overlay (never obscuring the full canvas).

2. **Given** the credits display,
   **when** the credit value changes,
   **then** a pulse animation plays: 150ms ease-out scale to 1.08 + brightness +20%, return over 300ms.

3. **Given** the hold capacity bar,
   **when** ore is deposited,
   **then** the bar fills left-to-right and pulses (subtle intensity) on each deposit; pulses emphatic intensity and shifts colour when hold is full.

4. **Given** the speed indicator,
   **when** updating,
   **then** it shows the live numeric value with no pulse (continuous change would be noisy).

5. **Given** the credits and hold `aria-live` regions,
   **when** values change,
   **then** screen readers announce the update (`role="status"`, `aria-live="polite"`).

6. **Given** `prefers-reduced-motion`,
   **when** enabled,
   **then** pulse animations are suppressed; values update without animation.

## Tasks / Subtasks

- [ ] Task 1 — Extend snapshot/state contracts for HUD data (AC: 1, 2, 3, 4)
  - [ ] Add credits and cargo-hold fields to the server→client snapshot contract used by `WorldStateUpdate` (ship-level or companion payload) in `server/BelterLife.Shared/Contracts/Hubs/`.
  - [ ] Populate new fields in `server/BelterLife.Simulation/Physics/SimulationLoop.cs` from authoritative server state.
  - [ ] Mirror contract additions in `client/src/types/index.ts` and `client/src/state/WorldState.ts` while preserving wire-casing normalization.

- [ ] Task 2 — Add HUD overlay scaffold and mounting lifecycle (AC: 1)
  - [ ] Create `client/src/ui/HudBottomBar.ts` as an HTML overlay component mounted above canvas (Layer 2 overlay), independent from PixiJS world rendering.
  - [ ] Mount/unmount the HUD from `client/src/app.ts` lifecycle so it appears during active gameplay sessions.
  - [ ] Ensure bar is fixed to viewport bottom, translucent, and non-blocking to core canvas interactions outside its own controls.

- [ ] Task 3 — Implement credits pulse behavior (AC: 2, 5, 6)
  - [ ] Create reusable pulse utility/component (`client/src/ui/PulseIndicator.ts` or equivalent) with `subtle` and `emphatic` intensity options.
  - [ ] Trigger credit pulse only on value changes after initial hydration (no first-render pulse).
  - [ ] Add `role="status"` + `aria-live="polite"` for credits value updates.
  - [ ] Respect `prefers-reduced-motion`: update value with no animation.

- [ ] Task 4 — Implement hold bar fill + pulse variants (AC: 3, 5, 6)
  - [ ] Render hold percentage as fill bar and numeric percent.
  - [ ] Trigger subtle pulse on incremental deposits and emphatic pulse + full-state visual shift at 100%.
  - [ ] Add `role="status"` + `aria-live="polite"` for hold updates.
  - [ ] Ensure full-state styling uses existing design tokens/theme primitives (no hardcoded new color system).

- [ ] Task 5 — Implement live speed indicator (AC: 4)
  - [ ] Compute or consume speed in a consistent unit (mm/s source; display conversion documented in UI component).
  - [ ] Update speed continuously without pulse animation.
  - [ ] Exclude speed from `aria-live` announcements to avoid excessive chatter.

- [ ] Task 6 — Add motion/accessibility behavior and tests (AC: 2, 3, 5, 6)
  - [ ] Add client tests for pulse trigger conditions (change-only, intensity switching, reduced-motion suppression).
  - [ ] Add tests for accessibility attributes (`role`, `aria-live`) on credits and hold, and explicit absence on speed.
  - [ ] Add tests verifying HUD remains visible through world updates and does not remount/reset on each tick.

- [ ] Task 7 — Verify build/test gates
  - [ ] Run `npm run build` in `client/`.
  - [ ] Run relevant client tests (`npm run test`) for HUD/pulse modules.
  - [ ] Run `dotnet build server/BelterLife.slnx` if server snapshot contracts were changed.

## Dev Notes

### Story Foundation (Epic 2)

- This story delivers the ambient HUD part of FR35 for Epic 2 and must align with UX spec behavior for bottom bar and pulse indicator.
- Keep scope constrained to persistent awareness UI (credits, hold %, speed) plus change-driven pulse feedback.
- Scanning/mining/docking interactions remain in later Epic 2 stories; this story only surfaces values and reactive cues.

### Technical Requirements (Must Follow)

- Keep server-authoritative state model:
  - Credits/cargo values shown in HUD must come from authoritative server snapshots, not local speculative state.
  - Speed display can be derived from authoritative velocity (`sqrt(vx² + vy²)`) client-side if no explicit speed field is sent.
- Maintain int64/mm world model established in Story 2.0:
  - Position remains `long` in contracts; no coordinate model regression.
  - Velocity units remain mm/s.
- Preserve SignalR naming/casing rules:
  - Server contracts in PascalCase; client normalized to camelCase at entry point.

### Architecture Compliance

- Respect two-layer UI architecture from UX/architecture docs:
  - Layer 1: PixiJS canvas for world rendering.
  - Layer 2: HTML overlay for HUD components.
- Do not move HUD concerns into PixiJS entity layers; keep it as HTML overlay UI.
- Keep feature placement aligned with current client structure:
  - UI logic under `client/src/ui/`
  - shared state ingress in `client/src/state/WorldState.ts`
  - app lifecycle wiring in `client/src/app.ts`

### Library / Framework Requirements

- PixiJS v8 remains renderer only; no DOM HUD implementation inside Pixi scene graph.
- Tailwind v4 + existing theme primitives should style the HUD; avoid introducing ad-hoc design system tokens.
- `prefers-reduced-motion` handling must follow established pattern already used in touch input code/tests.
- Keep TypeScript strictness constraints (`noUnusedLocals`, `noUnusedParameters`) satisfied in all new files.

### File Structure Requirements

- Expected server touchpoints (if contract extension is needed):
  - `server/BelterLife.Shared/Contracts/Hubs/ShipSnapshot.cs`
  - `server/BelterLife.Shared/Contracts/Hubs/WorldStateUpdate.cs` (only if adding a parallel HUD payload)
  - `server/BelterLife.Simulation/Physics/SimulationLoop.cs`
- Expected client touchpoints:
  - `client/src/types/index.ts`
  - `client/src/state/WorldState.ts`
  - `client/src/app.ts`
  - `client/src/ui/HudBottomBar.ts` (new)
  - `client/src/ui/PulseIndicator.ts` (new, if extracted)
  - `client/src/style.css` (if token-aligned HUD class additions are needed)

### Testing Requirements

- Client tests must validate:
  - Credits pulse triggers on change only.
  - Hold pulse subtle/emphatic behavior and full-state transition.
  - Speed updates with no pulse.
  - `prefers-reduced-motion` suppresses pulse animations.
  - Accessibility attributes and announcement scope (`credits`/`hold` only).
- If server contracts are changed, add/adjust simulation-loop tests ensuring HUD values are included in outgoing snapshots.

### Previous Story Intelligence (Story 2.1)

- Reuse existing `WorldStateUpdate` pipeline and avoid broad refactors outside snapshot/data flow and HUD overlay.
- Keep changes additive and localized; prior story succeeded by minimizing surface area and preserving architecture boundaries.
- Continue using documented conventions from `project-context.md` (SignalR casing, strict TS, no runtime data fixups).

### Git Intelligence Summary

- Git history output is not reliably available in this execution context; use current repository state and implemented Story 2.1 patterns as the immediate baseline.

### Latest Technical Information

- Repository-pinned stack versions (current local source of truth):
  - `pixi.js` `^8.16.0`
  - `@microsoft/signalr` `^10.0.0`
  - `@microsoft/signalr-protocol-msgpack` `^10.0.0`
  - `tailwindcss` `^4.2.0`
  - `typescript` `~5.9.3`
- No external web lookup was executed in this run; rely on pinned versions above unless intentionally upgraded in a dedicated dependency story.

### Project Structure Notes

- Current client already has minimal HTML overlay usage (notifications/auth screen) and an empty Pixi `UiLayer`; implementing HUD as HTML overlay aligns with both architecture and UX documents.
- No structure conflicts detected for this story.

### References

- `_bmad-output/planning-artifacts/epics.md` (Epic 2 / Story 2.2 definition and ACs)
- `_bmad-output/planning-artifacts/ux-design-specification.md` (HUD Bottom Bar, Pulse Indicator, accessibility and motion specs)
- `_bmad-output/planning-artifacts/architecture.md` (two-layer UI architecture, SignalR/client conventions)
- `project-context.md` (naming, int64/mm rules, TS/Pixi gotchas)
- `_bmad-output/implementation-artifacts/2-1-living-belt-asteroid-physics-simulation.md` (previous story implementation patterns)

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- Story context generated from BMAD create-story workflow artifacts for Epic 2 / Story 2.2.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story prepared with implementation guardrails for server-authoritative HUD state, pulse accessibility, and reduced-motion behavior.

### File List

- _bmad-output/implementation-artifacts/2-2-hud-bottom-bar-pulse-indicator.md

## Change Log

- 2026-03-01: Story created via BMAD create-story workflow; status set to `ready-for-dev`.