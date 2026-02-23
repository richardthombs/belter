# Story 1.7: Persistent Session — Return to Exact State

Status: done

## Story

As a **player**,
I want my ship position, sector, and credits saved automatically so that I return exactly where I left off with no penalty for time offline,
so that the game respects my time and builds a habit of return.

## Acceptance Criteria

1. **Given** a player with a ship at position (x, y) in sector S, **when** the player closes the browser or their session ends, **then** ship position, sector, and credits are durably persisted to the database — no session-termination hook is needed because `SimulationLoop` already writes `Ship` state to DB every tick.

2. **Given** a persisted player state, **when** the player logs back in and the client calls `POST /api/v1/players/me/spawn`, **then** `SpawnResponse` contains the actual saved `SpawnX` and `SpawnY` (not `0, 0`), and the client camera is positioned there immediately — before the first `WorldStateUpdate` arrives.

3. **Given** the saved spawn position is now occupied by an asteroid, **when** the shard's `SpawnController` checks on re-entry, **then** the ship is relocated to the nearest safe point (expanding search in 80 px steps, up to 800 px radius; fallback to sector origin), ship velocity is reset to zero, `SpawnResponse.Repositioned = true`, and the client displays the notification: *"Your ship was repositioned — the belt moved while you were away"*.

4. **Given** any length of time between sessions, **then** no assets (ship, credits) are lost or penalised — credits persist on the `players` table; cargo persistence is deferred to Epic 2 (no cargo system exists yet).

5. **Given** a server restart, **when** the shard process comes back up, **then** all player ship state (position, velocity, heading) and credits are fully recovered from PostgreSQL without any manual intervention — EF Core persistence already guarantees this.

## Tasks / Subtasks

- [x] Task 1 — Add `Credits` to `Player` entity and create EF migration (AC: 4)
  - [x] In `server/BelterLife.Shared/Entities/Player.cs`: add `public int Credits { get; set; }` property
  - [x] Run migration: `cd server && dotnet ef migrations add AddPlayerCredits --project BelterLife.Simulation --startup-project BelterLife.Simulation`
  - [x] Verify generated migration adds `credits integer NOT NULL DEFAULT 0` via `HasDefaultValue(0)` or check migration SQL
  - [x] Run `dotnet build server/BelterLife.slnx` → 0 errors

- [x] Task 2 — Extend `SpawnResponse` with `Repositioned` flag (AC: 3)
  - [x] In `server/BelterLife.Shared/Contracts/Api/SpawnResponse.cs`: change record to `record SpawnResponse(int SectorId, int ShipId, float SpawnX, float SpawnY, bool Repositioned = false)`
  - [x] Update `client/src/types/index.ts`: add `repositioned: boolean` to `SpawnResponse` interface
  - [x] Run `dotnet build server/BelterLife.slnx` and `cd client && npm run build` → 0 errors each

- [x] Task 3 — Fix `SpawnController` returning player path (AC: 2, 3, 4)
  - [x] In `server/BelterLife.Simulation/Api/SpawnController.cs`, rewrite the existing-player branch:
    - Load `Ship` from DB: `var ship = await _db.Ships.FirstOrDefaultAsync(s => s.Id == existing.ShipId);`
    - If `ship is null`, fall through to new-player creation (defensive; should never happen)
    - Load asteroids for sector: `var asteroids = await _db.Asteroids.Where(a => a.SectorId == existing.SectorId).AsNoTracking().ToListAsync();`
    - Perform overlap check and reposition (see Dev Notes for full algorithm)
    - Update `existing.LastSeenAt = DateTimeOffset.UtcNow;`
    - If repositioned OR LastSeenAt changed: `await _db.SaveChangesAsync();` — track `existing` (no `AsNoTracking`) so EF picks up the change
    - Return `Ok(new SpawnResponse(existing.SectorId, existing.ShipId, ship.X, ship.Y, repositioned))`
  - [x] For new-player path: set `Credits = 500` on the new `Player` entity (starting credits)
  - [x] `BelterLife.Gateway.Tests` and `BelterLife.Gateway.Tests/Hubs/GameHubTests.cs` require **no changes** — their `new SpawnResponse(...)` calls use a subset of positional args and `Repositioned` defaults to `false`
  - [x] Run `dotnet build server/BelterLife.slnx` → 0 errors

- [x] Task 4 — Client: initialise camera at saved spawn position (AC: 2)
  - [x] In `client/src/rendering/Renderer.ts`: add `initCameraAt(x: number, y: number): void` — see Dev Notes for exact implementation (uses `this.app.screen.width/height`, matching the existing `tick()` logic)
  - [x] In `client/src/app.ts`: after `await renderer.init(canvas)` and `renderer.setLocalShipId(...)`, add `renderer.initCameraAt(spawnResponse.spawnX, spawnResponse.spawnY)`
  - [x] Run `cd client && npm run build` → 0 TypeScript errors

- [x] Task 5 — Client: show repositioned notification (AC: 3)
  - [x] In `client/src/app.ts`: after `renderer.initCameraAt(...)`, add:
    ```typescript
    if (spawnResponse.repositioned) {
        showNotification("Your ship was repositioned — the belt moved while you were away");
    }
    ```
  - [x] Implement `showNotification(message: string)` as a simple DOM toast in `client/src/ui/Notification.ts`:
    - Creates a fixed-position `div` with `role="status"` and `aria-live="polite"` (screen reader accessible)
    - Styled: `position:fixed; top:16px; left:50%; transform:translateX(-50%); background:rgba(0,0,0,0.8); color:#fff; padding:12px 20px; border-radius:6px; z-index:200; font-size:14px`
    - Auto-removes after 5 000 ms via `setTimeout`
    - No animation (keep simple; UX animation system not yet established)
  - [x] Import and use in `app.ts`: `import { showNotification } from "./ui/Notification"`
  - [x] Run `cd client && npm run build` → 0 errors

- [x] Task 6 — Tests: returning player spawn position and reposition (AC: 2, 3)
  - [x] In `server/BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs`, add tests:
    - `Spawn_ReturningPlayer_ReturnsActualShipPosition()` — first spawn creates player at (0,0); manually update `Ship.X=50f, Ship.Y=50f` in DB (distance ≈ 71 units — safely inside the clear zone; `SectorGenerator` places asteroids at minimum 150 units from origin, so `<110 units` is always asteroid-free); second spawn returns `SpawnX=50f, SpawnY=50f` and `Repositioned=false`
    - `Spawn_ReturningPlayer_RepositionsShipWhenOverlapsAsteroid()` — create player; extract `sectorId` from first spawn response; insert `new Asteroid { SectorId = sectorId, X = 0f, Y = 0f, Radius = 100f, VertexCount = 6, RotationOffset = 0f }` into DB (must include `SectorId` to satisfy FK); second spawn returns `Repositioned=true` and a position that does NOT overlap the asteroid
    - `Spawn_NewPlayer_InitialisesCredits_500()` — verify new `Player.Credits == 500` after first spawn
  - [x] Run `cd server && dotnet test BelterLife.slnx` → all pass

- [x] Task 7 — Build verification (full stack)
  - [x] `cd server && dotnet build BelterLife.slnx` → 0 errors
  - [x] `cd server && dotnet test BelterLife.slnx` → 0 failures
  - [x] `cd client && npm run build` → 0 errors

### Review Follow-ups (AI)

- [ ] [AI-Review][LOW] Add test for fallback-to-origin reposition path (all 80 candidates blocked) — `server/BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs`
- [ ] [AI-Review][LOW] Add JSDoc or runtime guard to `Renderer.initCameraAt()` documenting that `init()` must be called first — `client/src/rendering/Renderer.ts:35`
- [ ] [AI-Review][LOW] Add inline comment to `SpawnController` reposition candidate loop noting the intentional 8-direction coarseness and the SectorGenerator minimum-distance guarantee — `server/BelterLife.Simulation/Api/SpawnController.cs:63`

## Dev Notes

### Why Most of This Story is Already Working (But Incomplete)

The architecture persists ship state automatically. Key insight: `SimulationLoop.TickAsync()` calls `db.SaveChangesAsync()` every 33 ms with EF tracked `Ship` entities — so `Ship.X`, `Ship.Y`, `Ship.VelocityX`, `Ship.VelocityY`, `Ship.Heading`, `Ship.AngularVelocity` are already current in PostgreSQL every tick. There is **no logout hook needed**.

The **only bug** preventing AC2 from working today: `SpawnController` returns `SpawnResponse(existing.SectorId, existing.ShipId, 0f, 0f)` for returning players instead of loading `Ship.X` and `Ship.Y`. This is a ~5-line fix.

### Asteroid Overlap Check Algorithm (`SpawnController`)

```csharp
const float SafeMargin = 10f;    // clearance buffer beyond asteroid radius
const float StepSize  = 80f;     // px per search step
const int   MaxSteps  = 10;      // max 800 px search radius before fallback

bool Overlaps(float x, float y) =>
    asteroids.Any(a => MathF.Pow(a.X - x, 2) + MathF.Pow(a.Y - y, 2)
                       < MathF.Pow(a.Radius + SafeMargin, 2));

bool repositioned = false;
if (Overlaps(ship.X, ship.Y))
{
    bool found = false;
    for (int step = 1; step <= MaxSteps && !found; step++)
    {
        float d = step * StepSize;
        (float dx, float dy)[] candidates =
            [(d,0),(−d,0),(0,d),(0,−d),(d,d),(−d,d),(d,−d),(−d,−d)];
        foreach (var (dx, dy) in candidates)
        {
            if (!Overlaps(ship.X + dx, ship.Y + dy))
            {
                ship.X += dx;
                ship.Y += dy;
                found = true;
                break;
            }
        }
    }
    if (!found) { ship.X = 0f; ship.Y = 0f; }  // fallback: sector origin
    ship.VelocityX = 0f;
    ship.VelocityY = 0f;
    ship.AngularVelocity = 0f;                  // no catapulting into asteroids
    await _db.SaveChangesAsync();
    repositioned = true;
}
```

Use C# `(-d, 0)` tuples: the candidates array syntax above is pseudocode — write as:
```csharp
var candidates = new (float, float)[]
    { (d,0f),(-d,0f),(0f,d),(0f,-d),(d,d),(-d,d),(d,-d),(-d,-d) };
```

### Credits Field — EF Migration Checklist

- Add `public int Credits { get; set; }` to `BelterLife.Shared/Entities/Player.cs`
- The migration tool targets `BelterLife.Simulation` (which owns `AppDbContext`) with `BelterLife.Simulation` as startup project
- Command (run from repo root): `cd server && dotnet ef migrations add AddPlayerCredits --project BelterLife.Simulation --startup-project BelterLife.Simulation`
- EF Core snake_case naming via `UseSnakeCaseNamingConvention()` will map `Credits` → `credits` column automatically — do NOT manually specify column name
- The column should default to `0` for existing rows — EF will handle this in the migration; verify the generated `Up()` method includes `.HasDefaultValue(0)` or add it manually if absent
- **IMPORTANT**: `UseSnakeCaseNamingConvention()` is in `AppDbContext.OnConfiguring` area and in `Program.cs` — do NOT remove or override it (breaks entire schema)
- **Tests do NOT need the migration** — `Program.cs` calls `db.Database.EnsureCreated()` in the `Testing` environment, which builds the schema directly from the current model. Adding `Credits` to `Player.cs` is sufficient for all tests to see the column in SQLite. Do not attempt to run `dotnet ef database update` against the test DB.

### `SpawnController` Dependency Injection

`SpawnController` already has `AppDbContext db` injected. No new services are needed for Task 3 — just use `_db` directly.

### `Player.LastSeenAt` — Update on Re-entry

The existing-player branch must update `existing.LastSeenAt = DateTimeOffset.UtcNow`. The `existing` entity is already EF-tracked (loaded via `FirstOrDefaultAsync` without `AsNoTracking`), so setting the property and calling `SaveChangesAsync` is enough. Combine this save with the reposition save — call `SaveChangesAsync` once covering both changes. Always call it unconditionally in the returning-player path (LastSeenAt always changes).

### Unchanged Gateway Test Files — No Edits Required

Two files construct `SpawnResponse` using positional args without `Repositioned`:
- `BelterLife.Gateway.Tests/GatewayIntegrationTests.cs`: `new SpawnResponse(SectorId: 1, ShipId: 42, SpawnX: 100f, SpawnY: 200f)` — compiles because `Repositioned = false` is a default parameter
- `BelterLife.Gateway.Tests/Hubs/GameHubTests.cs`: `new SpawnResponse(SectorId: 1, ShipId: 10, SpawnX: 0f, SpawnY: 0f)` — same reason

Do **not** touch these files. They require zero changes.

### Client Camera Positioning

Current flow in `app.ts`:
1. `spawnResponse = await spawn()` — REST call returns `{ sectorId, shipId, spawnX, spawnY, repositioned }`
2. `renderer.setLocalShipId(spawnResponse.shipId)` — tells WorldLayer which ship to follow
3. SignalR connection started → WorldStateUpdate drives camera every tick via `worldLayer.update()`

Without `initCameraAt()`, the camera starts at `(0, 0)` and snaps to the correct position on the first `WorldStateUpdate` tick (~33 ms). The snap is a single-frame jump — subtle but noticeable if the player was far from origin. `initCameraAt()` prevents this by pre-positioning the stage container before the first tick.

Implementation in `Renderer.ts`:
```typescript
initCameraAt(x: number, y: number): void {
    // Mirror the centering logic in tick():
    this.worldLayer.position.set(
        this.app.screen.width / 2 - x,
        this.app.screen.height / 2 - y,
    );
}
```

Call order in `app.ts` matters: call `initCameraAt` AFTER `renderer.init(canvas)` (which sets `app.screen.width/height`) and AFTER `renderer.setLocalShipId(...)`.

### `Notification.ts` — File Location and Style Rules

- File: `client/src/ui/Notification.ts` — export a named function `showNotification(message: string): void`
- Follow Story 1.6 precedent: use **inline `element.style` assignments** (no CSS file, no Tailwind classes) to avoid Tailwind purge conflicts with dynamically created DOM
- Mount to `document.body`, `z-index: 200` (above joystick at z-index 100, below future modals at ≥ 300)
- `role="status"` + `aria-live="polite"` satisfies accessibility requirement (consistent with UX design spec)
- Auto-remove after 5 000 ms: `setTimeout(() => el.remove(), 5000)`
- TypeScript: `noUnusedLocals: true` in tsconfig — ensure no unused variables in the implementation

### Cargo Persistence — Explicitly Out of Scope for Story 1.7

AC1 references "cargo" but there is no cargo system in Epic 1. Cargo will be introduced in Epic 2 (Story 2.5 — Asteroid Mining). When `CargoItem` entities are created and linked to ships/players, they will be persisted via the same EF Core pattern. **No placeholder or stub** is needed for cargo in this story — the schema will simply gain `cargo_items` table in Epic 2.

### `SpawnResponse` Record — Backward Compatibility

The C# record uses a default parameter `Repositioned = false` — this means existing callers (`ShardClient.SpawnAsync`, `GatewayIntegrationTests`) that don't use `Repositioned` continue to compile without changes. The TypeScript `SpawnResponse` interface adds `repositioned: boolean` — update `client/src/types/index.ts`.

### Testing Pattern — `SimulationWebApplicationFactory`

Established pattern (from Story 1.2/SpawnControllerTests): SQLite in-memory DB, `SimulationLoop` removed, `SHARD_SECRET=test-shard-secret` env var. All new tests MUST use `IClassFixture<SimulationWebApplicationFactory>`.

To seed ship position for `Spawn_ReturningPlayer_ReturnsActualShipPosition`:
```csharp
using var scope = _factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var ship = await db.Ships.FirstAsync(s => s.PlayerId == playerId);
ship.X = 50f;   // safe zone: <110 units from origin is always asteroid-free
ship.Y = 50f;   // SectorGenerator minimum asteroid dist = 150, max radius = 40
await db.SaveChangesAsync();
```

To seed the overlapping asteroid for `Spawn_ReturningPlayer_RepositionsShipWhenOverlapsAsteroid`:
```csharp
// Must include SectorId — FK constraint on asteroids table
db.Asteroids.Add(new Asteroid
{
    SectorId = firstSpawnResponse.SectorId,
    X = 0f, Y = 0f, Radius = 100f,
    VertexCount = 6, RotationOffset = 0f
});
await db.SaveChangesAsync();
```

Note: `SimulationWebApplicationFactory` shares one SQLite connection for the factory lifetime. Tests must use distinct `playerId` values (use `Guid.NewGuid()`) to avoid cross-test contamination.

### Server-Authoritative Position — No Client Override

AC5 (server restart recovery) relies entirely on EF persistence. The client NEVER submits a position — it only submits `InputEvent`. The `Ship` DB row is always the authoritative position. After a restart, the shard reloads ships from DB on the next tick; `SimulationLoop` reads all `Ship` rows with `ToListAsync()` every tick. No warm-up or manual repair needed. ✓

### Project Structure Notes

Files touched by this story:

```
server/
  BelterLife.Shared/
    Entities/
      Player.cs                       ← ADD Credits property
    Contracts/Api/
      SpawnResponse.cs                ← ADD Repositioned = false parameter
  BelterLife.Simulation/
    Api/
      SpawnController.cs              ← FIX returning player spawn + asteroid check
    Migrations/
      <timestamp>_AddPlayerCredits.cs ← NEW — generated by dotnet ef
  BelterLife.Simulation.Tests/
    Api/
      SpawnControllerTests.cs         ← ADD 3 new test methods

client/src/
  types/
    index.ts                          ← ADD repositioned: boolean to SpawnResponse
  rendering/
    Renderer.ts                       ← ADD initCameraAt() method
  ui/
    Notification.ts                   ← NEW — showNotification() toast
  app.ts                              ← USE spawnX/spawnY + show repositioned notif
```

No changes to: `GameHub.cs`, `ShardClient.cs`, `InputManager.ts`, `WorldState.ts`, `GameHubClient.ts`, `RestClient.ts`, `SimulationLoop.cs`, `PhysicsEngine.cs`.

### References

- Story requirements: [Source: epics.md#Story 1.7: Persistent Session — Return to Exact State]
- FR32–FR34 (persistent state + no offline penalty): [Source: epics.md#Functional Requirements]
- NFR6, NFR8 (zero data loss on restart, full recovery): [Source: architecture.md#Architecture Validation Results]
- PostgreSQL = single source of truth: [Source: architecture.md#Architectural Boundaries]
- `PlayerSession.cs` (FR31–34): [Source: architecture.md#BelterLife.Simulation/Entities/PlayerSession.cs] _(note: architecture planned `PlayerSession.cs` here but implementation has settled logic into `SpawnController.cs` + `SimulationLoop.cs` — this is consistent with the actual file structure)_
- `UseSnakeCaseNamingConvention()` — never override: [Source: project-context.md#Architecture Rules — Never Violate]
- `.slnx` not `.sln`: [Source: project-context.md#Toolchain Gotchas — .NET 10 Solution File Format]
- EF Core `IsRelational()` / in-memory check: [Source: project-context.md#Story Implementation Log — 1.2 Player auth]
- `SimulationWebApplicationFactory` test pattern: [Source: BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs]
- `noUnusedLocals: true` tsconfig guard: [Source: project-context.md#Toolchain Gotchas — TypeScript noUnusedLocals]
- Inline styles (no Tailwind for dynamic DOM): [Source: 1-6-dual-input-touch-virtual-joystick-keyboard-mouse.md#Libraries]
- `z-index` layering convention (joystick=100, notif=200, modals≥300): [Source: 1-6-dual-input-touch-virtual-joystick-keyboard-mouse.md#TouchInput DOM Management]
- `role="status"` + `aria-live="polite"` for value changes: [Source: epics.md#Story 2.2 HUD — credits aria-live]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6 (GitHub Copilot)

### Debug Log References

None — implementation clean on first pass.

### Code Review — 2026-02-23

**Reviewer:** Amelia (Dev Agent) — Claude Sonnet 4.6  
**Result:** APPROVED — 0 High · 3 Medium fixed · 3 Low deferred as action items

**Medium fixes applied:**
- M1: Fixed misleading comment in `SpawnController.cs` (`// fall through to new-player creation` → `// fail fast`) — [`SpawnController.cs:39`](server/BelterLife.Simulation/Api/SpawnController.cs)
- M2: Added `ship.Heading = 0f` reset on reposition to prevent re-entry into hazard — [`SpawnController.cs:78`](server/BelterLife.Simulation/Api/SpawnController.cs)
- M3: Added `el.style.pointerEvents = "none"` to toast to prevent input blocking during 5s display — [`Notification.ts:10`](client/src/ui/Notification.ts)

**Verification:** `dotnet test` → 55/55 passed; `npm run build` → 0 errors.

---

### Completion Notes List

- Task 1: `Player.Credits` added; EF migration `20260223082123_AddPlayerCredits.cs` generated with `defaultValue: 0` on `credits` column — confirmed in migration `Up()` method.
- Task 2: `SpawnResponse` extended with `Repositioned = false` default; `SpawnResponse` TypeScript interface updated with `repositioned: boolean`.
- Task 3: Existing-player branch fully rewritten — loads `Ship` from DB, runs asteroid overlap check with expanding search (8 cardinal/diagonal candidates, 80px steps, 10 max), resets velocity on reposition, updates `LastSeenAt`, calls `SaveChangesAsync()` once unconditionally. New-player path sets `Credits = 500`. Gateway test files untouched (default param handles backward compat).
- Task 4: `Renderer.initCameraAt()` added using same `worldLayer.position.set()` pattern as `tick()`. Called in `app.ts` after `setLocalShipId()`.
- Task 5: `client/src/ui/Notification.ts` created with inline styles (no Tailwind), `role="status"`, `aria-live="polite"`, 5-second auto-remove. Wired in `app.ts` after `initCameraAt()`.
- Task 6: Three new tests added — `Spawn_ReturningPlayer_ReturnsActualShipPosition`, `Spawn_ReturningPlayer_RepositionsShipWhenOverlapsAsteroid`, `Spawn_NewPlayer_InitialisesCredits_500`. All 55 tests (26 Simulation + 29 Gateway) pass.
- Task 7: `dotnet build` → 0 errors; `dotnet test` → 55/55 passed; `npm run build` → 0 TypeScript errors.
- Code Review (2026-02-23): M1 comment fix, M2 `Heading` reset on reposition, M3 `pointer-events:none` on toast. 55/55 tests still passing.

### File List

- `server/BelterLife.Shared/Entities/Player.cs` — added `Credits` property
- `server/BelterLife.Shared/Contracts/Api/SpawnResponse.cs` — added `Repositioned = false`
- `server/BelterLife.Simulation/Api/SpawnController.cs` — rewritten existing-player branch; new player gets Credits=500
- `server/BelterLife.Simulation/Migrations/20260223082123_AddPlayerCredits.cs` — generated migration
- `server/BelterLife.Simulation/Migrations/20260223082123_AddPlayerCredits.Designer.cs` — generated migration designer
- `server/BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs` — 3 new test methods + Asteroid using
- `client/src/types/index.ts` — `repositioned: boolean` on SpawnResponse
- `client/src/rendering/Renderer.ts` — `initCameraAt()` method
- `client/src/ui/Notification.ts` — NEW: `showNotification()` toast
- `client/src/app.ts` — `initCameraAt()` call + repositioned notification
