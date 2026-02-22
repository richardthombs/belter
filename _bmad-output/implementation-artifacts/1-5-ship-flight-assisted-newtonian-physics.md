# Story 1.5: Ship Flight — Assisted Newtonian Physics

Status: done

## Story

As a player,
I want to fly my ship using assisted Newtonian physics (thrust, momentum, and soft braking),
so that movement feels physical and responsive without requiring constant correction.

## Acceptance Criteria

1. **Given** the player's ship, **when** thrust input is applied via WASD or arrow keys, **then** the ship accelerates in the thrust direction — velocity accumulates via Newtonian physics (velocity += thrustForce * dt).
2. **Given** a moving ship with no thrust input, **when** no brake is engaged, **then** the ship maintains velocity unchanged (pure Newtonian drift) — to decelerate the player uses retro thrusters (Thrust = -1) or the Brake flag.
3. **Given** the ship at speed, **when** thrust is applied, **then** velocity accumulates in the ship's current heading direction via vector addition; heading is controlled independently by Torque input.
4. **Given** any client-submitted position or velocity state, **when** received by the server, **then** it is rejected — the server only accepts `InputEvent` vectors; all physics are server-authoritative (NFR12).
5. **Given** `SendInput` called on the SignalR hub with a valid `InputEvent`, **when** processed by the shard on the next tick, **then** the ship's physics state (position + velocity) is updated and reflected in the next `WorldStateUpdate`.
6. **Given** Torque input applied, **when** processed on the server, **then** the ship's angular velocity accumulates (with assisted damping when no torque), heading updates accordingly, and is visible to all connected clients via `WorldStateUpdate`.

## Tasks / Subtasks

- [x] Task 1 — Shared `InputEvent` contract (AC: 1, 3, 4, 5)
  - [x] Create `server/BelterLife.Shared/Contracts/Hubs/InputEvent.cs`:
    ```csharp
    namespace BelterLife.Shared.Contracts.Hubs;
    // Thrust: 1 = main engines (forward), -1 = retro thrusters (backward), 0 = off.
    // Torque: 1 = rotate right, -1 = rotate left, 0 = off.
    public record InputEvent(float Thrust, float Torque, bool Brake);
    ```
  - [x] Add `InputEvent` interface to `client/src/types/index.ts`:
    ```typescript
    export interface InputEvent {
        thrust: number;  // 1 = main engines, -1 = retros, 0 = off
        torque: number;  // 1 = rotate right, -1 = rotate left, 0 = off
        brake: boolean;
    }
    ```

- [x] Task 2 — Shard input buffer (AC: 4, 5)
  - [x] Create `server/BelterLife.Simulation/Entities/InputBuffer.cs`:
    - Interface `IInputBuffer` with `Set(string playerId, InputEvent input)` and `GetAll(): IReadOnlyDictionary<string, InputEvent>`
    - Concrete `InputBuffer` backed by `ConcurrentDictionary<string, InputEvent>` — `Set` overwrites immediately (last-write-wins, safe for 30fps); `GetAll` returns a snapshot dict
    - Register as `builder.Services.AddSingleton<IInputBuffer, InputBuffer>()` in `Program.cs`
  - [x] Create `server/BelterLife.Simulation/Api/InputController.cs`:
    - `POST /api/internal/input` — accepts `InputRequest { string PlayerId; InputEvent Input; }`
    - Validates `X-Shard-Secret` header (read from `SHARD_SECRET` env var — same guard pattern as `BroadcastController` in the Gateway)
    - Constructor throws `InvalidOperationException` when `SHARD_SECRET` is not configured
    - Calls `_inputBuffer.Set(request.PlayerId, request.Input)`, returns `204 No Content`
    - Place in `BelterLife.Simulation/Api/` — not under `Physics/`

- [x] Task 3 — PhysicsEngine (AC: 1, 2, 3, 6)
  - [x] Implement `server/BelterLife.Simulation/Physics/PhysicsEngine.cs`:
    - Make PhysicsEngine a singleton service (`builder.Services.AddSingleton<PhysicsEngine>()`)
    - Constants (public `const float` fields for test access):
      - `ThrustForce = 150f` (main engine acceleration, units/s²)
      - `RetroForce = 100f` (retro thruster acceleration, units/s²)
      - `MaxSpeed = 300f` (speed cap, units/s)
      - `AngularAccel = 4.0f` (angular acceleration, rad/s²)
      - `MaxAngularSpeed = 2.5f` (angular speed cap, rad/s)
      - `AngularDamping = 4.0f` (rotation braking coefficient, 1/s)
      - `BrakeDamping = 4.0f` (linear brake damping coefficient, 1/s)
    - `ApplyPhysics(Ship ship, InputEvent? input, float deltaSeconds)` — heading-based flight model:
      1. Rotation: apply torque → `ship.AngularVelocity += torque * AngularAccel * dt`, clamp to `MaxAngularSpeed`; when no torque, decay angular velocity via `AngularDamping`; integrate: `ship.Heading += ship.AngularVelocity * dt`
      2. Linear thrust in heading direction — facing vector: `(sin θ, −cos θ)`. Main engines (Thrust > 0) accelerate along facing; retros (Thrust < 0) decelerate. Zero thrust = pure Newtonian drift (velocity unchanged)
      3. Brake flag: apply strong linear damping (`BrakeDamping`) to both velocity components
      4. Clamp speed to `MaxSpeed`
      5. Integrate position: `ship.X += ship.VelocityX * dt; ship.Y += ship.VelocityY * dt`

- [x] Task 4 — SimulationLoop physics integration (AC: 5)
  - [x] Inject `IInputBuffer` and `PhysicsEngine` into `SimulationLoop` constructor
  - [x] Modify `TickAsync` to mutate ship state before broadcasting:
    1. Load ships **without** `AsNoTracking` (EF must track changes for `SaveChangesAsync`)
    2. Compute `float dt = _tickRateMs / 1000f`
    3. For each ship: call `_physicsEngine.ApplyPhysics(ship, _inputBuffer.GetAll().GetValueOrDefault(ship.PlayerId), dt)`
    4. `await db.SaveChangesAsync(cancellationToken)` before building snapshots
    5. Build `ShipSnapshot` list from updated ship positions (same as existing pattern)
    6. Broadcast as before
  - [x] Note: `AsNoTracking()` must be **removed** from the Ships query only (Asteroids can keep it)

- [x] Task 5 — Gateway: `IShardClient.SendInputAsync` + `GameHub.SendInput` (AC: 4, 5)
  - [x] Add to `server/BelterLife.Gateway/Infrastructure/IShardClient.cs`:
    ```csharp
    Task SendInputAsync(string playerId, InputEvent input);
    ```
  - [x] Implement in `server/BelterLife.Gateway/Infrastructure/ShardClient.cs`:
    - `POST /api/internal/input` with JSON body `{ playerId, input }` and `X-Shard-Secret` header
    - Mirror the existing `SpawnAsync` error-handling pattern — log and swallow on failure (input loss is acceptable at 30fps)
  - [x] Add `SendInput` hub method to `server/BelterLife.Gateway/Hubs/GameHub.cs`:
    ```csharp
    public async Task SendInput(InputEvent input)
    {
        var userId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null) return;
        await _shardClient.SendInputAsync(userId, input);
    }
    ```
  - [x] Add `using BelterLife.Shared.Contracts.Hubs;` to `GameHub.cs`

- [x] Task 6 — Client: `KeyboardInput` + `InputManager` (AC: 1, 2, 3)
  - [x] Implement `client/src/input/KeyboardInput.ts`:
    - Attach `keydown`/`keyup` listeners to `window` in constructor, `dispose()` removes them
    - Track active keys in a `Set<string>` using `event.code` (not `event.key`) for layout-independence
    - Codes to handle: `KeyW`, `ArrowUp`, `KeyS`, `ArrowDown`, `KeyA`, `ArrowLeft`, `KeyD`, `ArrowRight`
    - `getThrust(): number` — returns `1 | 0 | -1` (W/ArrowUp = 1 main engines, S/ArrowDown = -1 retros)
    - `getTorque(): number` — returns `1 | 0 | -1` (D/ArrowRight = 1 rotate right, A/ArrowLeft = -1 rotate left)
    - Call `event.preventDefault()` for arrow keys to suppress page scrolling
  - [x] Implement `client/src/input/InputManager.ts` — **event-driven** (not polling):
    - Constructor takes `GameHubClient` instance; creates `KeyboardInput` internally
    - Listens to `keydown`/`keyup` on `window`; fires `sendInput` only when thrust/torque state changes (debounced by key event)
    - `reconcile(serverThrust, serverTorque)` — called ~once/s when server includes input in `WorldStateUpdate`; re-sends if server state disagrees with current keyboard
    - `start()` — sends initial state baseline
    - `stop()` — removes event listeners, calls `keyboard.dispose()`

- [x] Task 7 — Client: `GameHubClient.sendInput` (AC: 5)
  - [x] Add `sendInput(input: InputEvent): void` to `client/src/network/GameHubClient.ts`:
    ```typescript
    sendInput(input: InputEvent): void {
        // ContractlessStandardResolver uses exact C# property names (PascalCase) on the wire.
        // Use send() (fire-and-forget), not invoke() — invoke() queues pending completions
        // which exhausts the SignalR pipeline at 20hz+.
        this.connection.send("SendInput", {
            Thrust: input.thrust,
            Torque: input.torque,
            Brake:  input.brake,
        }).catch(() => { /* swallow — input loss tolerable at event rate */ });
    }
    ```
  - [x] Add `import type { InputEvent } from "../types";` to `GameHubClient.ts`

- [x] Task 8 — Client: `app.ts` integration (AC: 1, 5)
  - [x] Import `InputManager` in `app.ts`
  - [x] After `await hubClient.start()`: instantiate `new InputManager(hubClient)` and call `.start()`

- [x] Task 9 — Tests (AC: 1, 2, 3, 4, 5)
  - [x] Create `server/BelterLife.Simulation.Tests/Physics/PhysicsEngineTests.cs`:
    - `ApplyPhysics_MainEngines_AcceleratesForwardAlongHeading()` — heading=0 (up), Thrust=1: vY < 0, vX=0
    - `ApplyPhysics_NoInput_VelocityUnchanged()` — pure Newtonian: vX=100 stays 100 with null input
    - `ApplyPhysics_RetroThrusters_OpposesHeading()` — moving up, Thrust=-1: vY increases toward 0
    - `ApplyPhysics_TorqueRight_AccumulatesAngularVelocity()` — Torque=1: AngularVelocity ≈ AngularAccel*dt, Heading > 0
    - `ApplyPhysics_NoTorque_AngularVelocityDecays()` — AngularVelocity=2.0, no input: decays but not instant zero
    - `ApplyPhysics_ExceedingMaxSpeed_ClampsToMaxSpeed()` — near MaxSpeed facing right, fire main engines: speed == MaxSpeed
    - `ApplyPhysics_MainEnginesAfterRotation_ThrustFollowsNewHeading()` — heading=π/2, Thrust=1: vX > 0, vY ≈ 0
  - [x] Update `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs`:
    - Add mock/stub for `IInputBuffer` returning empty snapshot; update `Tick_BroadcastsWorldStateUpdate_ForEachSector` to inject it
    - Add `Tick_WithInputBuffer_UpdatesShipPosition()` — seeds DB ship at (0,0) with heading=0, stubs input buffer with Thrust=1 for that playerId, runs one tick, verifies ship Y < 0 (moved upward in screen-space)
  - [x] Create `server/BelterLife.Gateway.Tests/Hubs/SendInputTests.cs`:
    - `SendInput_WhenUserAuthenticated_ForwardsToShardClient()` — mock `IShardClient.SendInputAsync`, invoke hub `SendInput`, verify called with correct playerId and InputEvent
    - `SendInput_WhenUserIdMissing_DoesNotCallShard()` — unauthenticated context, verify `SendInputAsync` never called
  - [x] `dotnet build server/BelterLife.slnx` → 0 errors
  - [x] `dotnet test server/BelterLife.slnx` → all tests passing
  - [x] `cd client && npm run build` → 0 TypeScript errors

## Dev Notes

### Architecture — This Story's Input Flow

```
[Client]                       [Gateway]                    [Shard]
   |                               |                            |
   | KB input poll (50ms)          |                            |
   | InputManager                  |                            |
   |-- SendInput (SignalR) ------->|                            |
   |   { Thrust, Torque, Brake }   |                            |
   |                               |-- SendInputAsync() ------->|
   |                               |   POST /api/internal/input |
   |                               |   X-Shard-Secret           |
   |                               |                 InputBuffer.Set(playerId, input)
   |                               |                            |
   |                               |         tick (33ms) -------|
   |                               |   PhysicsEngine.ApplyPhysics(ship, input, dt)
   |                               |   db.SaveChangesAsync()    |
   |                               |<-- POST /api/internal/broadcast
   |<-- WorldStateUpdate ----------|    (updated X, Y, VX, VY, Heading)
   |   PixiJS renders at new pos   |                            |
```

### Key Architecture Rules

- **Server-authoritative physics (NFR12)** — The client sends `InputEvent` only. There is no endpoint that accepts `x`, `y`, `velocityX`, or `velocityY` from the client. The shard computes all physics. [Source: architecture.md#Core Architectural Decisions → Server-authoritative physics]
- **`InputEvent` is a SignalR Hub argument, not a broadcast** — direction is Client→Server only; no `[MessagePackObject]` attributes needed since `ContractlessStandardResolver` serialises by property name [Source: Story 1.4 Dev Notes#SignalR MessagePack]
- **PascalCase wire keys for Hub invocation** — `ContractlessStandardResolver` uses exact C# property names. TypeScript must send `{ Thrust, Torque, Brake }` (PascalCase) when invoking `SendInput`. The `sendInput()` helper in `GameHubClient` handles this mapping. [Source: WorldState.ts normalizeKeys comment — same resolver applies in both directions]
- **Hub up = negative Y** — Screen-space: Y increases downward. Keyboard W/ArrowUp maps to `thrustY = -1` so that "up on screen = negative Y" is consistent with PixiJS coordinate system. PhysicsEngine uses this as-is. [Source: PixiJS v8 coordinate system; ShipRenderer.update() position.set(snapshot.x, snapshot.y)]
- **`InputBuffer.GetAll()` returns a snapshot** — the physics loop reads a consistent snapshot; concurrent writes from the HTTP endpoint modify the live dict, which is fine since ConcurrentDictionary is thread-safe.
- **EF tracking required on Ships** — `AsNoTracking()` must be removed from the Ships `db.Ships` query in `SimulationLoop.TickAsync` so `SaveChangesAsync` persists position updates. Asteroids can keep `AsNoTracking()`. [Source: EF Core tracking behaviour]
- **Input loss is acceptable** — at 50ms poll rate, an occasional lost frame on the network is tolerable. `sendInput()` uses fire-and-forget (`invoke().catch(() => {})`). `SendInputAsync` in `ShardClient` logs and swallows on HTTP failure.
- **`InputBuffer` is not cleared per tick** — the buffer holds the last known input from each player. If a player stops sending (tab loses focus), the last `InputEvent` is replayed until a zero-thrust event arrives and sets the buffer to `{ 0, 0, false }`. This is intentional: brief connection hiccups should not instantly stop the ship.

### `InputController` — Secret Guard Pattern

Mirrors the Gateway's `BroadcastController` (Story 1.4):

```csharp
// server/BelterLife.Simulation/Api/InputController.cs
[ApiController]
[Route("api/internal/input")]
public class InputController : ControllerBase
{
    readonly IInputBuffer _buffer;
    readonly string _secret;

    public InputController(IInputBuffer buffer, IConfiguration config)
    {
        _buffer = buffer;
        _secret = config["SHARD_SECRET"]
            ?? throw new InvalidOperationException("SHARD_SECRET is not configured");
    }

    [HttpPost]
    public IActionResult Post([FromBody] InputRequest request,
        [FromHeader(Name = "X-Shard-Secret")] string? secret)
    {
        if (secret != _secret) return Forbid();
        _buffer.Set(request.PlayerId, request.Input);
        return NoContent();
    }
}

public record InputRequest(string PlayerId, InputEvent Input);
```

### `ShardClient.SendInputAsync` — Pattern

Mirrors existing `SpawnAsync` in `ShardClient.cs`:

```csharp
public async Task SendInputAsync(string playerId, InputEvent input)
{
    try
    {
        var body = JsonContent.Create(new { playerId, input });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/input")
        {
            Content = body
        };
        request.Headers.Add("X-Shard-Secret", _secret);
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "SendInputAsync failed — input lost for player {PlayerId}", playerId);
    }
}
```

### TickAsync Changes Summary

The key change to `SimulationLoop.TickAsync`:

```csharp
internal async Task TickAsync(AppDbContext db, CancellationToken cancellationToken)
{
    var sectors   = await db.Sectors.AsNoTracking().ToListAsync(cancellationToken);
    if (sectors.Count == 0) return;

    var sectorIds = sectors.Select(s => s.Id).ToList();
    // NOTE: No AsNoTracking() here — EF must track ships for SaveChangesAsync
    var ships     = await db.Ships
        .Where(s => sectorIds.Contains(s.SectorId))
        .ToListAsync(cancellationToken);
    var asteroids = await db.Asteroids
        .AsNoTracking()
        .Where(a => sectorIds.Contains(a.SectorId))
        .ToListAsync(cancellationToken);

    float dt = _tickRateMs / 1000f;
    var inputs = _inputBuffer.GetAll();

    foreach (var ship in ships)
    {
        inputs.TryGetValue(ship.PlayerId, out var input);
        _physicsEngine.ApplyPhysics(ship, input, dt);
    }

    await db.SaveChangesAsync(cancellationToken);

    long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    foreach (var sector in sectors)
    {
        // ... build snapshots from updated ships and broadcast (existing pattern)
    }
}
```

### SimulationLoop Constructor Changes

```csharp
public SimulationLoop(
    IServiceScopeFactory scopeFactory,
    IGatewayClient gatewayClient,
    IInputBuffer inputBuffer,
    PhysicsEngine physicsEngine,
    IConfiguration config,
    ILogger<SimulationLoop> logger)
{
    _scopeFactory   = scopeFactory;
    _gatewayClient  = gatewayClient;
    _inputBuffer    = inputBuffer;
    _physicsEngine  = physicsEngine;
    _tickRateMs     = config.GetValue<int>("TickRateMs", 33);
    _logger         = logger;
}
```

### InputManager Polling Loop

```typescript
// client/src/input/InputManager.ts
import { KeyboardInput } from "./KeyboardInput";
import type { GameHubClient } from "../network/GameHubClient";

export class InputManager {
    private keyboard: KeyboardInput;
    private intervalId: ReturnType<typeof setInterval> | null = null;

    constructor(private hubClient: GameHubClient) {
        this.keyboard = new KeyboardInput();
    }

    start(intervalMs = 50): void {
        this.intervalId = setInterval(() => {
            this.hubClient.sendInput({
                thrustX: this.keyboard.getThrustX(),
                thrustY: this.keyboard.getThrustY(),
                brake:   false,
            });
        }, intervalMs);
    }

    stop(): void {
        if (this.intervalId !== null) clearInterval(this.intervalId);
        this.keyboard.dispose();
    }
}
```

### Heading Convention

`ShipRenderer` sets `this.rotation = snapshot.heading`. PixiJS rotation uses radians where `0` = pointing up (the triangle is drawn nose-up). `Math.atan2(y, x)` returns the angle from the +X axis. Applying thrust in direction `(nx, ny)`:

`ship.Heading = MathF.Atan2(ny, nx) - MathF.PI / 2f`

This converts "angle from +X axis" to "PixiJS rotation where 0 = up". When `nx=1, ny=0` (right): `Atan2(0,1) - π/2 = -π/2` → ship points right ✓. When `nx=0, ny=-1` (up on screen): `Atan2(-1,0) - π/2 = -π/2 - π/2 = -π` → ship points up ✓.

### NuGet Packages

No new NuGet packages required. All dependencies already installed.

**npm packages already installed:**
- `@microsoft/signalr` v10.0.0 ✓
- `@microsoft/signalr-protocol-msgpack` v10.0.0 ✓

### References

- Story ACs + user story: [Source: epics.md#Story 1.5: Ship Flight — Assisted Newtonian Physics]
- Server-authoritative physics: [Source: architecture.md#Core Architectural Decisions → Server-authoritative physics] and [Source: epics.md#NFR12]
- InputManager abstraction requirement: [Source: architecture.md#Frontend Architecture → Input abstraction]
- `InputEvent` shape: [Source: architecture.md#Frontend Architecture → Input abstraction — `InputEvent { thrust: Vector2; brake: boolean; interact: boolean }`]
- PascalCase SignalR hub method names: [Source: architecture.md#Implementation Patterns → SignalR Hub Methods]
- MessagePack ContractlessStandardResolver (PascalCase wire keys): [Source: Story 1.4 Dev Notes#SignalR MessagePack; client/src/state/WorldState.ts normalizeKeys()]
- PixiJS coordinate system (Y increases downward): [Source: ShipRenderer.ts — position.set(x, y), rotation = heading]
- BroadcastController secret guard pattern: [Source: server/BelterLife.Gateway/Hubs/BroadcastController.cs]
- ShardClient pattern: [Source: server/BelterLife.Gateway/Infrastructure/ShardClient.cs]
- EF Core tracking / AsNoTracking: [Source: Story 1.4 Dev Notes#SimulationLoop Scope Pattern — M4 review fix]
- `PeriodicTimer` pattern: [Source: Story 1.4 Dev Notes#SimulationLoop Scope Pattern]
- Client GATEWAY_URL: `http://gateway:80` (internal Docker port) [Source: docker-compose.yml; Story 1.4 bug fix 39ef757]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

- `InputManager.ts`: constructor parameter shorthand (`private hubClient`) banned by `erasableSyntaxOnly` tsconfig option — switched to explicit field + assignment.

### Completion Notes List

- All 52 server tests pass — 23 Simulation + 29 Gateway (`dotnet test` — 0 failures).
  - Includes 3 new `InputControllerTests` added during code review (secret validation, buffer interaction).
- Client TypeScript build clean (`npm run build` — 0 errors).
- `AsNoTracking()` removed from Ships query in `SimulationLoop.TickAsync` so EF tracks mutations for `SaveChangesAsync`.
- `InputBuffer` is a singleton — last-write-wins per player, never cleared. Zero-thrust events sent by client on key release trigger assisted braking.
- `SendInput` hub method added to `GameHub`; PascalCase wire mapping handled in `GameHubClient.sendInput()` to satisfy ContractlessStandardResolver.

### File List

**Created:**
- `server/BelterLife.Shared/Contracts/Hubs/InputEvent.cs`
- `server/BelterLife.Simulation/Entities/InputBuffer.cs`
- `server/BelterLife.Simulation/Api/InputController.cs`
- `server/BelterLife.Simulation.Tests/Physics/PhysicsEngineTests.cs`
- `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs` *(updated)*
- `server/BelterLife.Gateway.Tests/Hubs/SendInputTests.cs`
- `server/BelterLife.Simulation.Tests/Api/InputControllerTests.cs` *(added in code review)*

**Modified:**
- `server/BelterLife.Simulation/Physics/PhysicsEngine.cs`
- `server/BelterLife.Simulation/Physics/SimulationLoop.cs`
- `server/BelterLife.Simulation/Program.cs`
- `server/BelterLife.Gateway/Infrastructure/IShardClient.cs`
- `server/BelterLife.Gateway/Infrastructure/ShardClient.cs`
- `server/BelterLife.Gateway/Hubs/GameHub.cs`
- `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs`
- `client/src/types/index.ts`
- `client/src/input/KeyboardInput.ts`
- `client/src/input/InputManager.ts`
- `client/src/network/GameHubClient.ts`
- `client/src/app.ts`
