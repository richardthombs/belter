# Story 1.5: Ship Flight — Assisted Newtonian Physics

Status: ready-for-dev

## Story

As a player,
I want to fly my ship using assisted Newtonian physics (thrust, momentum, and soft braking),
so that movement feels physical and responsive without requiring constant correction.

## Acceptance Criteria

1. **Given** the player's ship, **when** thrust input is applied via WASD or arrow keys, **then** the ship accelerates in the thrust direction — velocity accumulates via Newtonian physics (velocity += thrustForce * dt).
2. **Given** a moving ship with no thrust input, **when** assisted flight is active, **then** the ship decelerates gently to rest (soft damping, not instant stop and not pure drift).
3. **Given** the ship at speed, **when** thrust is applied in a different direction, **then** the velocity vector changes correctly via vector addition (not heading-locked).
4. **Given** any client-submitted position or velocity state, **when** received by the server, **then** it is rejected — the server only accepts `InputEvent` vectors; all physics are server-authoritative (NFR12).
5. **Given** `SendInput` called on the SignalR hub with a valid `InputEvent`, **when** processed by the shard on the next tick, **then** the ship's physics state (position + velocity) is updated and reflected in the next `WorldStateUpdate`.
6. **Given** the ship heading, **when** thrust is applied, **then** the ship's heading updates to face the thrust direction and is visible to all connected clients via `WorldStateUpdate`.

## Tasks / Subtasks

- [ ] Task 1 — Shared `InputEvent` contract (AC: 1, 3, 4, 5)
  - [ ] Create `server/BelterLife.Shared/Contracts/Hubs/InputEvent.cs`:
    ```csharp
    namespace BelterLife.Shared.Contracts.Hubs;
    public record InputEvent(float ThrustX, float ThrustY, bool Brake);
    ```
  - [ ] Add `InputEvent` interface to `client/src/types/index.ts`:
    ```typescript
    export interface InputEvent {
        thrustX: number;
        thrustY: number;
        brake: boolean;
    }
    ```

- [ ] Task 2 — Shard input buffer (AC: 4, 5)
  - [ ] Create `server/BelterLife.Simulation/Entities/InputBuffer.cs`:
    - Interface `IInputBuffer` with `Set(string playerId, InputEvent input)` and `GetAll(): IReadOnlyDictionary<string, InputEvent>`
    - Concrete `InputBuffer` backed by `ConcurrentDictionary<string, InputEvent>` — `Set` overwrites immediately (last-write-wins, safe for 30fps); `GetAll` returns a snapshot dict
    - Register as `builder.Services.AddSingleton<IInputBuffer, InputBuffer>()` in `Program.cs`
  - [ ] Create `server/BelterLife.Simulation/Api/InputController.cs`:
    - `POST /api/internal/input` — accepts `InputRequest { string PlayerId; InputEvent Input; }`
    - Validates `X-Shard-Secret` header (read from `SHARD_SECRET` env var — same guard pattern as `BroadcastController` in the Gateway)
    - Constructor throws `InvalidOperationException` when `SHARD_SECRET` is not configured
    - Calls `_inputBuffer.Set(request.PlayerId, request.Input)`, returns `204 No Content`
    - Place in `BelterLife.Simulation/Api/` — not under `Physics/`

- [ ] Task 3 — PhysicsEngine (AC: 1, 2, 3, 6)
  - [ ] Implement `server/BelterLife.Simulation/Physics/PhysicsEngine.cs`:
    - Make PhysicsEngine a singleton service (`builder.Services.AddSingleton<PhysicsEngine>()`)
    - Constants (expose as `const float` fields for test access):
      - `ThrustForce = 150f` (units/s²)
      - `MaxSpeed = 300f` (units/s)
      - `BrakeDamping = 2.0f` (deceleration coefficient, 1/s — velocity multiplied by `1f - BrakeDamping * dt` per tick)
    - `ApplyPhysics(Ship ship, InputEvent? input, float deltaSeconds)`:
      1. Compute thrust: if `input` has any non-zero thrust (`ThrustX != 0 || ThrustY != 0`):
         - Normalise the thrust vector: `len = MathF.Sqrt(tx*tx + ty*ty); nx = tx/len; ny = ty/len`
         - `ship.VelocityX += nx * ThrustForce * deltaSeconds`
         - `ship.VelocityY += ny * ThrustForce * deltaSeconds`
         - Update heading to face thrust direction: `ship.Heading = MathF.Atan2(ny, nx) - MathF.PI / 2f` (PixiJS rotation: 0 = up; Atan2(y,x) measured from +X, subtract 90° to align the triangle nose)
      2. Assisted braking when no thrust (or `input.Brake == true`):
         - `float friction = 1f - BrakeDamping * deltaSeconds`
         - `ship.VelocityX *= MathF.Max(0f, friction)`
         - `ship.VelocityY *= MathF.Max(0f, friction)`
      3. Clamp to `MaxSpeed`:
         - `float speed = MathF.Sqrt(vx*vx + vy*vy); if (speed > MaxSpeed) { ship.VelocityX = vx/speed * MaxSpeed; ship.VelocityY = vy/speed * MaxSpeed; }`
      4. Integrate position: `ship.X += ship.VelocityX * deltaSeconds; ship.Y += ship.VelocityY * deltaSeconds`

- [ ] Task 4 — SimulationLoop physics integration (AC: 5)
  - [ ] Inject `IInputBuffer` and `PhysicsEngine` into `SimulationLoop` constructor
  - [ ] Modify `TickAsync` to mutate ship state before broadcasting:
    1. Load ships **without** `AsNoTracking` (EF must track changes for `SaveChangesAsync`)
    2. Compute `float dt = _tickRateMs / 1000f`
    3. For each ship: call `_physicsEngine.ApplyPhysics(ship, _inputBuffer.GetAll().GetValueOrDefault(ship.PlayerId), dt)`
    4. `await db.SaveChangesAsync(cancellationToken)` before building snapshots
    5. Build `ShipSnapshot` list from updated ship positions (same as existing pattern)
    6. Broadcast as before
  - [ ] Note: `AsNoTracking()` must be **removed** from the Ships query only (Asteroids can keep it)

- [ ] Task 5 — Gateway: `IShardClient.SendInputAsync` + `GameHub.SendInput` (AC: 4, 5)
  - [ ] Add to `server/BelterLife.Gateway/Infrastructure/IShardClient.cs`:
    ```csharp
    Task SendInputAsync(string playerId, InputEvent input);
    ```
  - [ ] Implement in `server/BelterLife.Gateway/Infrastructure/ShardClient.cs`:
    - `POST /api/internal/input` with JSON body `{ playerId, input }` and `X-Shard-Secret` header
    - Mirror the existing `SpawnAsync` error-handling pattern — log and swallow on failure (input loss is acceptable at 30fps)
  - [ ] Add `SendInput` hub method to `server/BelterLife.Gateway/Hubs/GameHub.cs`:
    ```csharp
    public async Task SendInput(InputEvent input)
    {
        var userId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null) return;
        await _shardClient.SendInputAsync(userId, input);
    }
    ```
  - [ ] Add `using BelterLife.Shared.Contracts.Hubs;` to `GameHub.cs`

- [ ] Task 6 — Client: `KeyboardInput` + `InputManager` (AC: 1, 2, 3)
  - [ ] Implement `client/src/input/KeyboardInput.ts`:
    - Attach `keydown`/`keyup` listeners to `window` in constructor, `dispose()` removes them
    - Track active keys in a `Set<string>` using `event.code` (not `event.key`) for layout-independence
    - Codes to handle: `KeyW`, `ArrowUp`, `KeyS`, `ArrowDown`, `KeyA`, `ArrowLeft`, `KeyD`, `ArrowRight`
    - `getThrustX(): number` — returns `-1 | 0 | 1` (left/right from A/D/ArrowLeft/ArrowRight)
    - `getThrustY(): number` — returns `-1 | 0 | 1` (up/down from W/S/ArrowUp/ArrowDown; **up = -1** to match screen-space convention used by the server)
    - Call `event.preventDefault()` for arrow keys to suppress page scrolling
  - [ ] Implement `client/src/input/InputManager.ts`:
    - Constructor takes `GameHubClient` instance
    - Creates a `KeyboardInput` instance internally
    - `start(intervalMs = 50)` — starts a `setInterval` poll loop:
      1. Read `thrustX`, `thrustY` from `KeyboardInput`
      2. If any non-zero: build `InputEvent { thrustX, thrustY, brake: false }` and call `_hubClient.sendInput(inputEvent)`
      3. Also send on every tick (not just on change) so server-side buffers always have fresh state; send a zero `InputEvent` when releasing so braking kicks in on the shard
    - Actually: send every poll tick regardless (zero thrust triggers assisted braking on server)
    - `stop()` — clears interval, calls `keyboardInput.dispose()`

- [ ] Task 7 — Client: `GameHubClient.sendInput` (AC: 5)
  - [ ] Add `sendInput(input: InputEvent): void` to `client/src/network/GameHubClient.ts`:
    ```typescript
    sendInput(input: InputEvent): void {
        // ContractlessStandardResolver uses exact C# property names (PascalCase) on the wire.
        this.connection.invoke("SendInput", {
            ThrustX: input.thrustX,
            ThrustY: input.thrustY,
            Brake:   input.brake,
        }).catch(() => { /* swallow — input loss tolerable at poll rate */ });
    }
    ```
  - [ ] Add `import type { InputEvent } from "../types";` to `GameHubClient.ts`

- [ ] Task 8 — Client: `app.ts` integration (AC: 1, 5)
  - [ ] Import `InputManager` in `app.ts`
  - [ ] After `await hubClient.start()`: instantiate `new InputManager(hubClient)` and call `.start()`

- [ ] Task 9 — Tests (AC: 1, 2, 3, 4, 5)
  - [ ] Create `server/BelterLife.Simulation.Tests/Physics/PhysicsEngineTests.cs`:
    - `ApplyPhysics_WithThrust_AccumulatesVelocity()` — ship starts at rest, one tick of full UP thrust, vY < 0 (upward), |velocity| ≈ ThrustForce * dt
    - `ApplyPhysics_NoThrust_ReducesVelocity()` — ship starts with vX=100, no input, one tick, vX < 100 and vX > 0 (braking but not instant stop)
    - `ApplyPhysics_ThrustInDifferentDirection_VectorAdds()` — ship moving right (vX=100), apply UP thrust, result: vX still 100, vY decreases (negative), confirming vector addition not heading lock
    - `ApplyPhysics_ExceedingMaxSpeed_ClampsToMaxSpeed()` — ship starts with vX=290, apply rightward thrust, result: speed == MaxSpeed
    - `ApplyPhysics_HeadingUpdates_ToFaceThrustDirection()` — after thrust right, heading ≈ Atan2(0,1) converted to PixiJS rotation
  - [ ] Update `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs`:
    - Add mock/stub for `IInputBuffer` returning empty snapshot; update `Tick_BroadcastsWorldStateUpdate_ForEachSector` to inject it
    - Add `Tick_WithInputBuffer_UpdatesShipPositions()` — seeds DB ship at (0,0), stubs input buffer with full rightward thrust for that playerId, runs one tick, verifies ship X > 0 in DB
  - [ ] Create `server/BelterLife.Gateway.Tests/Hubs/SendInputTests.cs`:
    - `SendInput_WhenUserAuthenticated_ForwardsToShardClient()` — mock `IShardClient.SendInputAsync`, invoke hub `SendInput`, verify called with correct playerId and InputEvent
    - `SendInput_WhenUserIdMissing_DoesNotCallShard()` — unauthenticated context, verify `SendInputAsync` never called
  - [ ] `dotnet build server/BelterLife.slnx` → 0 errors
  - [ ] `dotnet test server/BelterLife.slnx` → all tests passing
  - [ ] `cd client && npm run build` → 0 TypeScript errors

## Dev Notes

### Architecture — This Story's Input Flow

```
[Client]                       [Gateway]                    [Shard]
   |                               |                            |
   | KB input poll (50ms)          |                            |
   | InputManager                  |                            |
   |-- SendInput (SignalR) ------->|                            |
   |   { ThrustX, ThrustY, Brake } |                            |
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
- **PascalCase wire keys for Hub invocation** — `ContractlessStandardResolver` uses exact C# property names. TypeScript must send `{ ThrustX, ThrustY, Brake }` (PascalCase) when invoking `SendInput`. The `sendInput()` helper in `GameHubClient` handles this mapping. [Source: WorldState.ts normalizeKeys comment — same resolver applies in both directions]
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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

**Created:**
- `server/BelterLife.Shared/Contracts/Hubs/InputEvent.cs`
- `server/BelterLife.Simulation/Entities/InputBuffer.cs`
- `server/BelterLife.Simulation/Api/InputController.cs`
- `server/BelterLife.Simulation.Tests/Physics/PhysicsEngineTests.cs`
- `server/BelterLife.Gateway.Tests/Hubs/SendInputTests.cs`

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
