# Story 1.6: Dual Input — Touch Virtual Joystick & Keyboard/Mouse

Status: ready-for-dev

## Story

As a **player**,
I want to control my ship using a virtual joystick on tablet or WASD/arrow keys on desktop,
so that I can play comfortably on my preferred device with no difference in capability.

## Acceptance Criteria

1. **Given** a touch device, **when** the player holds their thumb on the virtual joystick zone (bottom-left, thumb-reach), **then** a visual joystick appears and ship thrust is applied proportional to displacement direction and magnitude.

2. **Given** a keyboard, **when** WASD or arrow keys are held, **then** the corresponding thrust `InputEvent` is sent to the server (existing behaviour — must not regress).

3. **Given** either input method, **when** processed through `InputManager`, **then** both produce the same `InputEvent { thrust: number; torque: number; brake: boolean }` — no game code branches on input type.

4. **Given** the virtual joystick touch target, **then** it is at least 48×48px.

5. **Given** `prefers-reduced-motion`, **when** enabled, **then** the joystick nub renders without animation (appear/disappear only — no transition).

## Tasks / Subtasks

- [ ] Task 1 — Implement `TouchInput.ts` (AC: 1, 3, 4, 5)
  - [ ] Replace existing stub `client/src/input/TouchInput.ts` with full implementation
  - [ ] Maintain exported type: `export type TouchChangeCallback = (thrust: number, torque: number) => void;`
  - [ ] Constructor: `constructor(onChange: TouchChangeCallback)` — creates joystick DOM elements and attaches event listeners
  - [ ] Build DOM dynamically (no static HTML required):
    ```
    <div class="joystick-zone">    <!-- 120×120px, fixed bottom:32px left:32px, touch capture zone -->
      <div class="joystick-ring">  <!-- full inset, border circle, semi-transparent -->
        <div class="joystick-nub"> <!-- 48×48px thumb indicator, centered, draggable -->
    ```
  - [ ] Style via inline `element.style` assignments (no CSS file — avoids ordering concerns with Tailwind/PixiJS overlay):
    - Zone: `position:fixed; bottom:32px; left:32px; width:120px; height:120px; border-radius:50%; touch-action:none; z-index:100`
    - Ring: `position:absolute; inset:0; border-radius:50%; border:2px solid rgba(255,255,255,0.4); background:rgba(255,255,255,0.05)`
    - Nub: `width:48px; height:48px; border-radius:50%; background:rgba(255,255,255,0.6); position:absolute; top:50%; left:50%; transform:translate(-50%,-50%)`
    - Nub transition: `transition: transform 0.05s ease-out` unless `prefers-reduced-motion` — then `transition: none`
  - [ ] Read `prefers-reduced-motion` via `window.matchMedia("(prefers-reduced-motion: reduce)").matches` in constructor; store as `private reducedMotion: boolean`
  - [ ] Mount zone to `document.body` in constructor
  - [ ] `maxRadius`: `(120 - 48) / 2 = 36` px — maximum nub displacement from centre
  - [ ] `deadZone`: `5` px — displacement below this returns 0 (prevents drift)
  - [ ] Touch tracking:
    - `touchstart` on zone element (with `{ passive: false }` to allow `preventDefault()`): capture `touch.identifier`, compute zone centre from `getBoundingClientRect()`
    - `touchmove` on `window` (passive: false): find tracked identifier in `changedTouches`, compute `dx/dy` from centre, clamp to `maxRadius`, normalise to [-1, 1]
    - `touchend`/`touchcancel` on `window`: release tracked identifier, reset nub to centre, call `onChange(0, 0)`
    - Only track ONE touch per `TouchInput` instance — ignore multi-touch (ignore additional touch starts if `activeTouch !== null`)
  - [ ] Direction mapping (screen-space → game-space):
    - Y axis: `thrust = -normalizedDy` (push thumb UP = negative screen dY = `thrust > 0` = forward; push DOWN = `thrust < 0` = retros)
    - X axis: `torque = normalizedDx` (push thumb RIGHT = `torque > 0` = rotate right; LEFT = `torque < 0` = rotate left)
    - Apply dead zone per axis independently: if `|displacement| < deadZone` → 0
    - Normalise: `clampedDist = min(dist, maxRadius)`, `normalised = clampedDist / maxRadius * sign`
    - Clamp final output to [-1, 1]
  - [ ] `_updateNub(offsetX: number, offsetY: number)`: sets `nub.style.transform = \`translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px))\``
  - [ ] `dispose()`: remove all event listeners, call `zone.remove()`

- [ ] Task 2 — Update `InputManager.ts` to integrate `TouchInput` (AC: 2, 3)
  - [ ] Import `TouchInput` from `./TouchInput`
  - [ ] Add private fields: `private touch: TouchInput`, `private touchThrust = 0`, `private touchTorque = 0`
  - [ ] In constructor: instantiate `new TouchInput((thrust, torque) => { this.touchThrust = thrust; this.touchTorque = torque; this.sendIfChanged(); })`
  - [ ] Add private helpers:
    ```typescript
    private currentThrust(): number {
        return Math.max(-1, Math.min(1, this.keyboard.getThrust() + this.touchThrust));
    }
    private currentTorque(): number {
        return Math.max(-1, Math.min(1, this.keyboard.getTorque() + this.touchTorque));
    }
    ```
  - [ ] Replace all uses of `this.keyboard.getThrust()` / `this.keyboard.getTorque()` in `sendIfChanged()`, `sendNow()`, and `reconcile()` with `this.currentThrust()` / `this.currentTorque()`
  - [ ] In `stop()`: add `this.touch.dispose()`
  - [ ] Do NOT change the `lastSent` tracking logic — threshold-delta behaviour is unchanged

- [ ] Task 3 — Verify `app.ts` requires no changes (AC: 3)
  - [ ] Confirm `InputManager` is constructed and `start()` called after `await hubClient.start()` — no changes needed; `TouchInput` is created internally by `InputManager`
  - [ ] Confirm `reconcile()` call in `onWorldStateUpdate` still works — no changes needed

- [ ] Task 4 — Build verification (AC: 2)
  - [ ] `cd client && npm run build` → 0 TypeScript errors, 0 Vite errors
  - [ ] `dotnet build server/BelterLife.slnx` → 0 errors (sanity check — no server files changed)

## Dev Notes

### Input Model

The game uses a heading-relative flight model: thrust accelerates in the ship's facing direction; torque rotates the ship. The virtual joystick maps:

```
Joystick up   (thumb Y < 0 screen)  →  thrust = +1  (main engines, forward)
Joystick down (thumb Y > 0 screen)  →  thrust = -1  (retro thrusters)
Joystick right (thumb X > 0 screen) →  torque = +1  (rotate clockwise)
Joystick left  (thumb X < 0 screen) →  torque = -1  (rotate counter-clockwise)
```

This matches keyboard: W/↑ = forward, S/↓ = retros, D/→ = rotate right, A/← = rotate left.

The joystick provides **analog** values (e.g. `thrust = 0.65`, `torque = 0.3`) rather than the keyboard's discrete `-1/0/1`. The server `PhysicsEngine.ApplyPhysics()` accepts `float Thrust` and `float Torque` and already handles all values in [-1, 1] — partial displacement from a soft joystick push gives proportional acceleration.

### No Server Changes Required

The server `InputEvent` C# record (`BelterLife.Shared.Contracts.Hubs.InputEvent`) already accepts `float Thrust, float Torque, bool Brake`. Analog values from the joystick work without any server-side changes.

`GameHubClient.sendInput()` already maps to `{ Thrust, Torque, Brake }` PascalCase wire format for `ContractlessStandardResolver` — no changes needed.

### `InputManager` Combination Strategy

When both inputs are active simultaneously (e.g., touchscreen laptop with external keyboard), thrust and torque values are **summed and clamped** to [-1, 1]:

```typescript
currentThrust() = clamp(keyboard.getThrust() + touchThrust, -1, 1)
currentTorque() = clamp(keyboard.getTorque() + touchTorque, -1, 1)
```

This handles edge cases gracefully: two sources pushing in the same direction still cap at full thrust; opposing inputs partially cancel. On a normal tablet there is no keyboard, so `keyboard.getThrust()` always returns 0 and touch values pass straight through.

### `TouchInput` DOM Management

`TouchInput` manages its own lifecycle — it creates the DOM elements in the constructor and removes them in `dispose()`. No changes to `index.html` are needed.

The joystick zone is mounted to `document.body` with `z-index: 100`, sitting above the PixiJS canvas (which has no `z-index`), but below any future modal overlays (which should use z-index ≥ 200). The zone uses `touch-action: none` to suppress native scroll/zoom gestures.

### `touchmove` Must Be on `window` (Not the Zone)

Once a touch begins, the browser will route `touchmove` to the element where `touchstart` fired. However, on some mobile browsers, fast swipes can escape the target element's bounds. Attaching `touchmove` and `touchend` to `window` guarantees continued tracking regardless of how far the thumb drifts. The handler filters by `activeTouch` identifier to avoid responding to other concurrent touches (scrolling UI panels, etc.).

### `prefers-reduced-motion`

The only animation in `TouchInput` is the `transition: transform 0.05s ease-out` on the nub. When `prefers-reduced-motion: reduce` is detected, this transition is set to `none` so the nub teleports to its computed position rather than animating. The joystick zone itself appears/disappears instantly (no `opacity` fade was specified).

### Joystick Dimensions and Touch Target

| Element | Size | Purpose |
|---|---|---|
| Zone (`div.joystick-zone`) | 120×120px | Touch capture area (always visible) |
| Ring (`div.joystick-ring`) | Fills zone (120×120px) | Visual outer boundary |
| Nub (`div.joystick-nub`) | 48×48px | Thumb indicator; meets ≥48×48px touch target AC |

`maxRadius = (120 - 48) / 2 = 36px` — the distance from zone centre to the furthest the nub can travel while staying fully inside the ring.

### `interact` Field — Intentionally Deferred

The architecture document originally specified `InputEvent { thrust: Vector2; brake: boolean; interact: boolean }`. The in-flight implementation simplified thrust/torque to scalars (matching the server's `float Thrust, float Torque` record) and deferred `interact`. This is correct — `interact` will be added to both the C# record and TypeScript interface when the first interaction mechanic (e.g., docking, scanning) is implemented in Epic 2.

### Reference: InputManager Before/After

**Before (keyboard only):**
```typescript
private sendNow(): void {
    const thrust = this.keyboard.getThrust();
    const torque = this.keyboard.getTorque();
    this.lastSent = { thrust, torque };
    this.hubClient.sendInput({ thrust, torque, brake: false });
}
```

**After (keyboard + touch combined):**
```typescript
private currentThrust(): number {
    return Math.max(-1, Math.min(1, this.keyboard.getThrust() + this.touchThrust));
}
private currentTorque(): number {
    return Math.max(-1, Math.min(1, this.keyboard.getTorque() + this.touchTorque));
}
private sendNow(): void {
    const thrust = this.currentThrust();
    const torque = this.currentTorque();
    this.lastSent = { thrust, torque };
    this.hubClient.sendInput({ thrust, torque, brake: false });
}
```

### Project Structure

Files touched are entirely within `client/src/input/`. No PixiJS, no SignalR, no server changes. The rendering layer, WorldState, and network layer are untouched.

```
client/src/input/
  InputManager.ts    ← MODIFIED (integrate TouchInput)
  KeyboardInput.ts   ← unchanged
  TouchInput.ts      ← REPLACED (was empty stub)
```

### Libraries

No new npm packages required. The virtual joystick is pure DOM/CSS — no third-party joystick library. Rationale: the joystick logic is ~100 lines, the UX requirements are specific (bottom-left fixed position, specific sizing), and adding a library would introduce versioning risk for minimal gain.

**Installed packages (no changes needed):**
- `pixi.js` v8 ✓ (not involved in this story — joystick is HTML overlay, not canvas)
- `@microsoft/signalr` v10 ✓
- `tailwindcss` v4 ✓ (not used in TouchInput — inline styles chosen to avoid Tailwind purge conflicts with dynamically added classes)

### References

- Story user story + ACs: [Source: epics.md#Story 1.6: Dual Input — Touch Virtual Joystick & Keyboard/Mouse]
- FR2 (touch + keyboard/mouse controls): [Source: epics.md#FR2]
- `InputManager` normalises both inputs → unified `InputEvent`: [Source: architecture.md#Frontend Architecture — Input abstraction]
- `InputManager abstraction must be in place before any input-dependent game features`: [Source: architecture.md#Decision Impact Analysis — Cross-Component Dependencies]
- File naming conventions (`PascalCase.ts` for class files): [Source: architecture.md#Naming Patterns — TypeScript Code; project-context.md#Critical Naming Conventions]
- Touch targets ≥44×44px, 48×48px preferred: [Source: ux-design-specification.md#Touch targets; epics.md#Story 1.6 AC: virtual joystick touch target]
- `prefers-reduced-motion`: appear/disappear only: [Source: epics.md#Story 1.6 AC: prefers-reduced-motion; epics.md#UX — Interaction Patterns]
- Design lead device: iPad portrait (~768px wide): [Source: ux-design-specification.md#Browser & Device Design Principles]
- Input principle: all interactions completable by touch alone: [Source: ux-design-specification.md#Input principle]
- `InputEvent { thrust, torque, brake }` wire format (PascalCase): [Source: 1-5-ship-flight-assisted-newtonian-physics.md#Key Architecture Rules; client/src/network/GameHubClient.ts — sendInput()]
- Joystick bottom-left fixed position thumb-reach: [Source: epics.md#Story 1.6 AC: "virtual joystick zone (bottom-left, thumb-reach)"]
- PixiJS Layer structure (Stage → UILayer): [Source: architecture.md#Frontend Architecture — Rendering layers] — joystick is HTML overlay, NOT a PixiJS object
- Previous story InputManager (event-driven, reconcile pattern): [Source: 1-5-ship-flight-assisted-newtonian-physics.md#Task 6 — Client: InputManager]
- `touchAction: none` DOM requirement for custom touch gestures: MDN Web API — TouchEvent
- `touch.identifier` for multi-touch disambiguation: MDN Web API — Touch.identifier

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
