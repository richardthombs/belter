# Story 1.4: Real-Time Game World via SignalR

Status: complete

## Story

As a **player**,
I want to see my ship and the asteroid sector rendered in real time in my browser,
So that I am connected to and experiencing the live game world.

## Acceptance Criteria

1. **Given** a logged-in player, **When** the client connects to the SignalR `GameHub` (WSS with JWT as `access_token` query param), **Then** the connection is established and the player is added to the SignalR group for their sector

2. **Given** the established connection, **When** the server sends `WorldStateUpdate` messages at the configured tick rate, **Then** the PixiJS canvas renders ship position and asteroid positions in real time

3. **Given** the PixiJS render loop, **When** running on a mid-range tablet, **Then** the client maintains 30–60 FPS (NFR1) — the renderer must not block the main thread per tick

4. **Given** the `SimulationLoop.cs` `IHostedService`, **When** running, **Then** it broadcasts `WorldStateUpdate` to the Gateway at a stable 30 FPS tick rate (~33ms interval) (NFR2)

5. **Given** all WebSocket traffic, **Then** it is encrypted via TLS/WSS (NFR14) — met by K8s ingress TLS termination; no application code required

6. **Given** the rendering stage, **When** rendering, **Then** layers render in order: BackgroundLayer → WorldLayer → EffectsLayer → UILayer

## Tasks / Subtasks

- [x] Task 1 — Define `WorldStateUpdate` message contract in Shared + TypeScript types (AC: 2)
  - [x] Create `server/BelterLife.Shared/Contracts/Hubs/ShipSnapshot.cs`: `record ShipSnapshot(int ShipId, string PlayerId, float X, float Y, float VelocityX, float VelocityY, float Heading)`
  - [x] Create `server/BelterLife.Shared/Contracts/Hubs/AsteroidSnapshot.cs`: `record AsteroidSnapshot(int AsteroidId, float X, float Y, float Radius, int VertexCount, float RotationOffset)`
  - [x] Create `server/BelterLife.Shared/Contracts/Hubs/WorldStateUpdate.cs`: `record WorldStateUpdate(int SectorId, long Timestamp, List<ShipSnapshot> Ships, List<AsteroidSnapshot> Asteroids)` — `Timestamp` is Unix milliseconds
  - [x] Update `client/src/types/index.ts`: add `ShipSnapshot`, `AsteroidSnapshot`, `WorldStateUpdate` interfaces matching C# record field names in camelCase

- [x] Task 2 — Implement `GatewayClient` in Simulation (pushes tick data to Gateway) (AC: 4)
  - [x] Create `server/BelterLife.Simulation/Infrastructure/GatewayClient.cs` as a typed `HttpClient`:
    - Constructor: `HttpClient http, IConfiguration config, ILogger<GatewayClient> logger`
    - Reads `GATEWAY_URL` (falls back to `http://gateway:5080`) and `SHARD_SECRET` config values
    - `Task BroadcastAsync(WorldStateUpdate update)` — POST to `/api/internal/broadcast`, `X-Shard-Secret` header, `JsonContent.Create(update)`; log warning on non-2xx, rethrow
  - [x] Register in `server/BelterLife.Simulation/Program.cs`: `builder.Services.AddHttpClient<GatewayClient>(c => c.BaseAddress = new Uri(builder.Configuration["GATEWAY_URL"] ?? "http://gateway:5080"))`
  - [x] Add `GATEWAY_URL: "http://gateway:5080"` to `shard` service `environment` in `docker-compose.yml`

- [x] Task 3 — Implement `BroadcastController` on Gateway (receives shard push, broadcasts via SignalR) (AC: 2)
  - [x] Create `server/BelterLife.Gateway/Hubs/BroadcastController.cs` with `[ApiController]`, `[Route("api/internal")]`
  - [x] Constructor: `IHubContext<GameHub> hubContext, IConfiguration config`
  - [x] `[HttpPost("broadcast")]` — validate `X-Shard-Secret` header (same pattern as `SpawnController` — return `StatusCode(403)` on mismatch); call `hubContext.Clients.Group($"sector-{update.SectorId}").SendAsync("WorldStateUpdate", update)`; return `Ok()`
  - [x] Note: `IHubContext<T>` is provided by ASP.NET Core SignalR — no new package needed

- [x] Task 4 — Implement `GameHub.cs` connection lifecycle (AC: 1)
  - [x] Add `[Authorize]` attribute to `GameHub` class
  - [x] Override `OnConnectedAsync`: extract `userId` from `Context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)`, call `shardClient.SpawnAsync(userId)` (idempotent — returns existing sector if already spawned), add connection to group `$"sector-{response.SectorId}"` via `Groups.AddToGroupAsync`; handle null response with `Context.Abort()`
  - [x] Override `OnDisconnectedAsync`: call `Groups.RemoveFromGroupAsync` (SignalR auto-removes on disconnect but explicit removal is cleaner); call `base.OnDisconnectedAsync(exception)`
  - [x] Constructor: inject `IShardClient shardClient`
  - [x] Note: JWT query-param extraction (`OnMessageReceived`) is ALREADY wired in `server/BelterLife.Gateway/Auth/IdentitySetup.cs` line 77–83 — do NOT add it again

- [x] Task 5 — Implement `SimulationLoop.cs` with real tick at 30 FPS + broadcast (AC: 4)
  - [x] Replace stub `ExecuteAsync` with a `PeriodicTimer` loop targeting `TICK_RATE_MS` (default 33ms, ~30 FPS)
  - [x] On each tick: load sectors + ships + asteroids from `AppDbContext`; for each sector build `WorldStateUpdate { SectorId, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Ships = [...], Asteroids = [...] }`; call `gatewayClient.BroadcastAsync(update)` (fire-and-forget on exception — tick must not crash on a single broadcast failure; log and continue)
  - [x] Read `TICK_RATE_MS` from `IConfiguration` (default 33); add to `appsettings.json` as `"TickRateMs": 33`
  - [x] Constructor: `AppDbContext db, GatewayClient gatewayClient, IConfiguration config, ILogger<SimulationLoop> logger`
  - [x] Note: `AppDbContext` in `BackgroundService` requires a scoped lifetime workaround — inject `IServiceScopeFactory`, create a scope per tick

- [x] Task 6 — Implement `WorldState.ts` and wire `GameHubClient.ts` (AC: 2)
  - [x] Update `client/src/state/WorldState.ts`:
    - Add typed fields: `let ships: ShipSnapshot[] = []`, `let asteroids: AsteroidSnapshot[] = []`, `let timestamp = 0`
    - Export `apply(update: WorldStateUpdate): void` — mutates module state
    - Export `getShips(): readonly ShipSnapshot[]` and `getAsteroid(): readonly AsteroidSnapshot[]`
  - [x] Update `client/src/network/GameHubClient.ts`:
    - Add `start(): Promise<void>` — calls `this.connection.start()`
    - Add `onWorldStateUpdate(handler: (update: WorldStateUpdate) => void): void` — registers handler via `this.connection.on('WorldStateUpdate', handler)`
    - Wire in `app.ts`: after hub start, call `client.onWorldStateUpdate(update => WorldState.apply(update))`

- [x] Task 7 — Implement `Renderer.ts` with PixiJS v8 init and layer composition (AC: 3, 6)
  - [x] Update `client/src/rendering/Renderer.ts`:
    - Add `private app: Application` (PixiJS Application — import from `pixi.js`)
    - Add `async init(canvas: HTMLCanvasElement): Promise<void>` — calls `await this.app.init({ canvas, resizeTo: window, backgroundColor: 0x0a0a1a })` (PixiJS v8 breaking change: `Application.init()` is async)
    - After init: create `BackgroundLayer`, `WorldLayer`, `EffectsLayer`, `UiLayer` instances; add to `this.app.stage` as children in that order
    - Add `start(): void` — `this.app.ticker.add((ticker) => this.tick(ticker.deltaTime))`
    - Add `private tick(delta: number): void` — calls `worldLayer.update(delta)` (other layers are static for this story)
    - Expose `getWorldLayer(): WorldLayer`

- [x] Task 8 — Implement `WorldLayer.ts`, `ShipRenderer.ts`, `AsteroidRenderer.ts` (AC: 2, 3, 6)
  - [x] Update `client/src/rendering/layers/WorldLayer.ts`:
    - Extends `Container` (PixiJS); constructor adds children for ship + asteroid containers
    - `update(): void` — calls `WorldState.getShips()` and `WorldState.getAsteroid()`, diffs against current child map, creates/updates/removes `ShipRenderer` and `AsteroidRenderer` instances keyed by id
  - [x] Update `client/src/rendering/entities/ShipRenderer.ts`:
    - Extends `Container`; uses `Graphics` to draw a triangle (heading-up pointer): `g.moveTo(0,-12).lineTo(8,10).lineTo(-8,10).closePath().fill(0x00ff88)`
    - `update(snapshot: ShipSnapshot): void` — set `position.set(snapshot.x, snapshot.y)`, `rotation = snapshot.heading`
  - [x] Update `client/src/rendering/entities/AsteroidRenderer.ts`:
    - Extends `Container`; uses `Graphics` to draw polygon from `VertexCount`, `Radius`, `RotationOffset`: generate vertex list from polar coords equally spaced with slight radius jitter seeded from `AsteroidId`; call `g.poly(points).fill(0x888888)`
    - Call `g.cacheAsTexture(true)` after drawing (static shape — `cacheAsTexture` is the PixiJS v8 method, replaces v7 `cacheAsBitmap`)
    - `update(snapshot: AsteroidSnapshot): void` — set `position.set(snapshot.x, snapshot.y)`

- [x] Task 9 — Wire `app.ts`: init Renderer + SignalR (AC: 1, 2, 3)
  - [x] Update `client/src/app.ts`:
    - Import `Renderer`, `GameHubClient`, `WorldState`, `spawn` (RestClient)
    - `app()`: call `await spawn()` to ensure sector is assigned, then `await renderer.init(canvas)`, `renderer.start()`, `await hubClient.start()`, wire `onWorldStateUpdate`
    - Add `spawn` export to `client/src/network/RestClient.ts`: `export async function spawn(): Promise<SpawnResponse>` — POST `/api/v1/players/me/spawn` with auth header, return parsed `SpawnResponse`
    - Add `SpawnResponse` interface to `client/src/types/index.ts`: `{ sectorId: number; shipId: number; spawnX: number; spawnY: number }`

- [x] Task 10 — Write tests (AC: 1, 2, 4)
  - [x] Create `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs`:
    - `Tick_BroadcastsWorldStateUpdate_ForEachSector()` — seeds DB with one sector (asteroids + ship), runs one tick, verifies `IGatewayClient.BroadcastAsync` called with matching SectorId and entity counts
    - Use `IGatewayClient` interface (create it: `BelterLife.Simulation/Infrastructure/IGatewayClient.cs`) + Moq
  - [x] Create `server/BelterLife.Gateway.Tests/Hubs/BroadcastControllerTests.cs`:
    - `Broadcast_WithValidSecret_CallsHubContext()` — mocks `IHubContext<GameHub>`, verifies `SendAsync("WorldStateUpdate", ...)` called on correct group `$"sector-{sectorId}"`
    - `Broadcast_MissingSecret_Returns403()`
  - [x] Create `server/BelterLife.Gateway.Tests/Hubs/GameHubTests.cs`:
    - `OnConnectedAsync_AddsConnectionToSectorGroup()` — mocks `IShardClient` returning `SpawnResponse(sectorId:1, ...)`, verify `Groups.AddToGroupAsync` called with `"sector-1"`
    - `OnConnectedAsync_AbortsWhenShardUnavailable()` — SpawnAsync returns null → `Context.Abort()` called

- [x] Task 11 — End-to-end verification (AC: 1, 2, 3, 4)
  - [x] `dotnet build server/BelterLife.slnx` → 0 errors
  - [x] `dotnet test server/BelterLife.slnx` → all tests passing
  - [x] `cd client && npm run build` → 0 TypeScript errors

## Dev Notes

### Architecture — This Story's Flow

```
[Client]                 [Gateway]                [Simulation Shard]
   |                        |                            |
   |-- spawn (REST POST) -->|-- SpawnAsync (HTTP) ------>|
   |<-- SpawnResponse ------|<-- 201 SpawnResponse ------|
   |                        |                            |
   |-- WS /hubs/game ------>|                            |
   |   (JWT query param)    |-- OnConnectedAsync         |
   |                        |   SpawnAsync(userId)       |
   |                        |   Group.Add("sector-1")    |
   |                        |                            |
   |                        |           tick (33ms) -----|
   |                        |<-- POST /api/internal/broadcast
   |                        |   (X-Shard-Secret)         |
   |                        |-- IHubContext.Group("sector-1")
   |<-- WorldStateUpdate ---|    .SendAsync(...)         |
   |  (PixiJS renders)      |                            |
```

### Key Architecture Rules

- **Server → Client SignalR messages: `PascalCase`** — `WorldStateUpdate`, not `worldStateUpdate`. [Source: architecture.md#SignalR Hub Methods]
- **SignalR game message timestamps: Unix milliseconds (integer)**, not ISO 8601. [Source: architecture.md#Format Patterns]
- **Hub lives on Gateway; physics loop lives on Simulation** — these are two separate services. The shard POSTs to the Gateway; there is no direct WebSocket from client to shard. [Source: architecture.md#Architectural Boundaries]
- **`IHubContext<GameHub>` enables server-side broadcast** — inject it into `BroadcastController`. It is automatically available in ASP.NET Core Web SDK — no extra package needed.
- **Groups named `sector-{sectorId}`** — consistent naming used by both `GameHub.OnConnectedAsync` and `BroadcastController`.
- **`[Authorize]` on `GameHub`** — JWT validated before `OnConnectedAsync` runs. JWT passed as `?access_token=...` query param on WebSocket upgrade. This is already configured in `AddBelterIdentity` if it follows standard SignalR JWT setup (see pattern below).
- **`SpawnAsync` is idempotent** — `GameHub.OnConnectedAsync` calls it to discover the player's sector even if already spawned. [Source: Story 1.3 AC1]
- **`AppDbContext` in `BackgroundService`** — DbContext is scoped, BackgroundService is singleton. MUST use `IServiceScopeFactory` to create a scope per tick; never inject `AppDbContext` directly into `SimulationLoop`. [Source: Microsoft.Extensions.DependencyInjection scoping rules]
- **`PeriodicTimer`** — the correct .NET 6+ pattern for background loops. Does not drift like `Task.Delay`. Available in `System.Threading`.
- **PixiJS v8 breaking changes from v7:**
  - `Application.init()` is async — must `await app.init({...})`
  - `cacheAsBitmap` removed → use `cacheAsTexture(true)` on Container/Graphics
  - `Graphics.poly()` draws filled polygon from point array

### JWT Query Param for SignalR — Already Configured ✅

`OnMessageReceived` is already wired in `server/BelterLife.Gateway/Auth/IdentitySetup.cs` (lines 77–83). It extracts `?access_token=` query param for all requests to paths starting with `/hubs`. **Do NOT add another `OnMessageReceived` handler.**

### GatewayClient Registration Pattern

```csharp
// server/BelterLife.Simulation/Program.cs
builder.Services.AddHttpClient<GatewayClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["GATEWAY_URL"] ?? "http://gateway:5080"));
```

Matches the `ShardClient` pattern in Gateway Program.cs. [Source: Story 1.3 Dev Notes — ShardClient Configuration]

### IGatewayClient Interface (required for Moq in tests)

```csharp
// server/BelterLife.Simulation/Infrastructure/IGatewayClient.cs
public interface IGatewayClient
{
    Task BroadcastAsync(WorldStateUpdate update);
}
```

Register as: `builder.Services.AddHttpClient<GatewayClient>(...).AddTypedClient<IGatewayClient, GatewayClient>()` — same pattern as Story 1.3's IShardClient. [Source: Story 1.3 Debug Log]

### SimulationLoop Scope Pattern

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(tickRateMs));
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            await TickAsync(db, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tick threw an exception — continuing");
        }
    }
}
```

### AsteroidRenderer Polygon Pattern

```typescript
// deterministic vertex jitter from asteroidId seed
function buildPolygonPoints(snapshot: AsteroidSnapshot): number[] {
  const points: number[] = [];
  for (let i = 0; i < snapshot.vertexCount; i++) {
    const angle = (i / snapshot.vertexCount) * Math.PI * 2 + snapshot.rotationOffset;
    const r = snapshot.radius * (0.8 + 0.2 * ((snapshot.asteroidId * 17 + i * 31) % 100) / 100);
    points.push(Math.cos(angle) * r, Math.sin(angle) * r);
  }
  return points;
}
```

### WorldLayer Diff Pattern

To avoid recreating Graphics objects each tick, maintain maps by id:

```typescript
private shipMap = new Map<number, ShipRenderer>();
private asteroidMap = new Map<number, AsteroidRenderer>();

update(): void {
  const ships = WorldState.getShips();
  // Remove stale
  for (const [id, r] of this.shipMap) {
    if (!ships.find(s => s.shipId === id)) { this.removeChild(r); this.shipMap.delete(id); }
  }
  // Add/update
  for (const ship of ships) {
    let r = this.shipMap.get(ship.shipId);
    if (!r) { r = new ShipRenderer(); this.addChild(r); this.shipMap.set(ship.shipId, r); }
    r.update(ship);
  }
  // Same for asteroids
}
```

### docker-compose Changes

Under `shard` service `environment`:
```yaml
GATEWAY_URL: "http://gateway:5080"
```

Under `shard` `appsettings.json` root:
```json
"TickRateMs": 33
```

### NFR14 Note

TLS/WSS is terminated at the Kubernetes ingress — no application code required for this AC. Mark it complete with a note. [Source: architecture.md#NFR Coverage]

### SignalR MessagePack — Already Registered

`builder.Services.AddSignalR().AddMessagePackProtocol()` is already in `Gateway/Program.cs`. Do NOT add it again. [Source: server/BelterLife.Gateway/Program.cs]

### NuGet Packages

No new NuGet packages required for any project in this story. All dependencies are in the Web SDK or already installed.

**npm packages already installed:**
- `@microsoft/signalr` v10.0.0 ✓
- `@microsoft/signalr-protocol-msgpack` v10.0.0 ✓
- `pixi.js` v8.16.0 ✓

### References

- Story ACs + user story: [Source: epics.md#Story 1.4: Real-Time Game World via SignalR]
- SignalR hub location: [Source: architecture.md#BelterLife.Gateway/Hubs/]
- GameHub.cs existing stub: [Source: server/BelterLife.Gateway/Hubs/GameHub.cs]
- GameHubClient.ts existing stub: [Source: client/src/network/GameHubClient.ts]
- WorldState.ts existing stub: [Source: client/src/state/WorldState.ts]
- SimulationLoop.cs existing stub: [Source: server/BelterLife.Simulation/Physics/SimulationLoop.cs]
- Rendering layer order: [Source: architecture.md#Frontend Architecture → Rendering layers]
- PixiJS v8, cacheAsTexture: [Source: architecture.md#Rendering approach]
- State management approach: [Source: architecture.md#Frontend Architecture → State management]
- SignalR JWT query param: [Source: architecture.md#Authentication & Security → SignalR auth]; already implemented at [server/BelterLife.Gateway/Auth/IdentitySetup.cs](server/BelterLife.Gateway/Auth/IdentitySetup.cs#L77)
- SignalR message naming: [Source: architecture.md#SignalR Hub Methods]
- Timestamp format: [Source: architecture.md#Format Patterns]
- SpawnAsync idempotency: [Source: Story 1.3 AC1]
- IShardClient/ShardClient typed client pattern: [Source: Story 1.3 Dev Notes + Debug Log]
- ProviderName guard pattern: [Source: Story 1.3 Dev Notes — ProviderName guard]; note: Gateway/Program.cs still uses old InMemory check — do NOT replicate in new code; use Environment check
- NFR14 TLS/WSS: [Source: architecture.md#NFR Coverage]
- NFR1 client FPS, NFR2 server tick: [Source: epics.md#Story 1.4 ACs]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

- `SimulationLoop.cs`: `replace_string_in_file` duplicated TickAsync body — fixed by removing the duplicate trailing block.
- `BroadcastControllerTests.cs`: `SendAsync` is a `ClientProxyExtensions` extension method and cannot be mocked with Moq — switched to `SendCoreAsync` which is the actual interface method on `IClientProxy`.
- `SimulationLoopTests.cs`: `IServiceScopeFactory` not found — added `using Microsoft.Extensions.DependencyInjection;`.

### Code Review Fixes (2026-02-22)

- **H1** `GameHub.cs`: Added `_sectorGroup` field; `OnDisconnectedAsync` now calls `Groups.RemoveFromGroupAsync` before `base.OnDisconnectedAsync`.
- **M1** Story File List: Added `Auth/IdentitySetup.cs` and `Gateway/Program.cs` (both modified in the 1.4 commit — RFC 9457 `OnChallenge` handler + `AddProblemDetails`).
- **M2** `SimulationLoop.ExecuteAsync`: Moved `scope` and `db` resolution inside the `try` block so DI errors are caught and logged rather than killing the `BackgroundService`.
- **M3** `BroadcastController`: Constructor now throws `InvalidOperationException` when `SHARD_SECRET` is not configured. Test `Constructor_ThrowsWhenSecretNotConfigured` added.
- **M4** `SimulationLoop.TickAsync`: Added `.AsNoTracking()` to all three queries; `Ships` and `Asteroids` filtered by `sectorIds.Contains(...)` to push WHERE clause to the DB rather than in-memory. Added early-return when no sectors.
- **L3** `GameHubTests`: Added `OnConnectedAsync_AbortsWhenUserIdMissing` test.
- **L4** `SimulationLoopTests`: `Tick_BroadcastsWorldStateUpdate_ForEachSector` assertion now includes `u.Timestamp > 0`.
- **New test** `GameHubTests.OnDisconnectedAsync_RemovesConnectionFromSectorGroup` — validates H1 fix.

### Completion Notes List

- NFR14 (TLS/WSS): Met by K8s ingress TLS termination — no application code required.
- `WorldState.ts`: Exported both `getAsteroids()` (plural, canonical) and kept `WorldState` namespace alias for backward compat. `WorldLayer.ts` uses `getAsteroids()`.
- `AsteroidRenderer.ts`: `cacheAsTexture(true)` called on Container (PixiJS v8 method replacing v7 `cacheAsBitmap`).
- `SimulationLoop.TickAsync`: Made `internal` with `InternalsVisibleTo("BelterLife.Simulation.Tests")` to enable direct unit testing without running the PeriodicTimer.
- `BroadcastController.cs`: Placed in `BelterLife.Gateway/Hubs/` namespace per story spec.

### File List

**Created:**
- `server/BelterLife.Shared/Contracts/Hubs/ShipSnapshot.cs`
- `server/BelterLife.Shared/Contracts/Hubs/AsteroidSnapshot.cs`
- `server/BelterLife.Shared/Contracts/Hubs/WorldStateUpdate.cs`
- `server/BelterLife.Simulation/Infrastructure/IGatewayClient.cs`
- `server/BelterLife.Simulation/Infrastructure/GatewayClient.cs`
- `server/BelterLife.Gateway/Hubs/BroadcastController.cs`
- `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs`
- `server/BelterLife.Gateway.Tests/Hubs/BroadcastControllerTests.cs`
- `server/BelterLife.Gateway.Tests/Hubs/GameHubTests.cs`

**Modified:**
- `server/BelterLife.Gateway/Hubs/GameHub.cs`
- `server/BelterLife.Gateway/Auth/IdentitySetup.cs`
- `server/BelterLife.Gateway/Program.cs`
- `server/BelterLife.Simulation/Physics/SimulationLoop.cs`
- `server/BelterLife.Simulation/Program.cs`
- `server/BelterLife.Simulation/appsettings.json`
- `docker-compose.yml`
- `client/src/types/index.ts`
- `client/src/state/WorldState.ts`
- `client/src/network/GameHubClient.ts`
- `client/src/network/RestClient.ts`
- `client/src/rendering/Renderer.ts`
- `client/src/rendering/layers/BackgroundLayer.ts`
- `client/src/rendering/layers/WorldLayer.ts`
- `client/src/rendering/layers/EffectsLayer.ts`
- `client/src/rendering/layers/UiLayer.ts`
- `client/src/rendering/entities/ShipRenderer.ts`
- `client/src/rendering/entities/AsteroidRenderer.ts`
- `client/src/app.ts`
- `client/src/main.ts`
