# Belter Life — Project Context

This file is loaded automatically by every dev agent at the start of each story (`dev-story` workflow).
Add findings here when you discover anything that will save future agents from repeating mistakes.

---

## Stack Quick Reference

| Layer | Technology |
|---|---|
| Server — simulation | .NET 10 Worker Service (`BelterLife.Simulation`) |
| Server — gateway | .NET 10 ASP.NET Core (`BelterLife.Gateway`) |
| Server — admin | .NET 10 ASP.NET Core (`BelterLife.Admin`) |
| Shared contracts | .NET 10 Class Library (`BelterLife.Shared`) |
| Database | PostgreSQL 16 (DigitalOcean Managed) via Npgsql EF Core |
| Real-time | ASP.NET Core SignalR + MessagePack protocol |
| Auth | ASP.NET Core Identity + JWT (query param on SignalR upgrade) |
| Client | Vite + vanilla TypeScript + PixiJS v8 |
| Client state | Plain TypeScript module (`WorldState.ts`) — no reactive framework |
| Client real-time | `@microsoft/signalr` + `@microsoft/signalr-protocol-msgpack` |
| Infra | Docker + Kubernetes (DOKS) + GitHub Actions |

---

## Critical Naming Conventions

Violating these will break the entire stack — enforce on every file you touch.

| Context | Convention | Example |
|---|---|---|
| C# classes / methods / properties | `PascalCase` | `PhysicsEngine`, `GetRegion()` |
| C# local vars / parameters / private fields | `camelCase` (no `_` prefix) | `tickRate`, `connectionString` |
| TypeScript class files | `PascalCase.ts` | `WorldState.ts`, `GameHubClient.ts` |
| TypeScript module files | `camelCase.ts` | `inputManager.ts` |
| TypeScript classes | `PascalCase` | `class GameHubClient` |
| TypeScript functions / variables | `camelCase` | `function updateState()` |
| JSON on the wire (REST + SignalR) | `camelCase` | `{ "playerId": "..." }` |
| REST timestamps | ISO 8601 UTC string | `"2026-02-21T14:00:00Z"` |
| SignalR game message timestamps | Unix milliseconds (integer) | `1708524000000` |
| PostgreSQL table names | `snake_case` plural | `players`, `navigation_catalogue_entries` |
| PostgreSQL column names | `snake_case` | `player_id`, `recorded_at` |
| SignalR Server→Client messages | `PascalCase` | `WorldStateUpdate`, `EntityHandoff` |
| SignalR Client→Server methods | `PascalCase` | `SendInput`, `InitiateJump` |
| REST route parameters | `{id}` (ASP.NET Core default) | `/api/v1/players/{id}` |
| REST query parameters | `camelCase` | `?playerId=...` |
| Error responses | RFC 9457 Problem Details | |

**`UseSnakeCaseNamingConvention()` is applied to `AppDbContext` in `Simulation/Program.cs`. Never remove or override it.**

---

## World Coordinate System — Critical

Decided 2026-02-23. Every story from Epic 2 onwards must follow these rules.

| Rule | Value |
|---|---|
| Position fields (`X`, `Y`) on all entities | `long` (int64) — 1 unit = 1 mm |
| Velocity fields (`VelocityX`, `VelocityY`) | `float` mm/s |
| Force/acceleration constants | `float` mm/s² |
| Position integration | `ship.X += (long)(ship.VelocityX * deltaSeconds)` |
| Sector size | 50km × 50km — `RegionBounds.SectorSize = 50_000_000L` |
| Sector grid address | `Sector.GridX`, `Sector.GridY` — both `long` |
| Lazy generation | `Sector.IsGenerated` — false until first player arrival triggers `SectorGenerator` |

**PhysicsEngine constants (DO NOT change without a story):**
```
MaxSpeed    = 300_000f   // mm/s = 300 m/s
ThrustForce = 150_000f   // mm/s²
RetroForce  = 100_000f   // mm/s²
AngularAccel, MaxAngularSpeed, AngularDamping, BrakeDamping — unchanged (radians / dimensionless)
```

**RegionBounds (canonical — never hardcode sector size inline):**
```csharp
RegionBounds.SectorSize = 50_000_000L  // 50km
RegionBounds.HalfSector = 25_000_000L  // ±25km local range per sector
```

**PostgreSQL migration gotcha — float→bigint requires USING clause:**
EF Core does not auto-generate the `USING x::bigint` cast for column type changes. If migrating a live DB with existing data, add manually:
```csharp
migrationBuilder.Sql("ALTER TABLE asteroids ALTER COLUMN x TYPE bigint USING x::bigint");
```
SQLite (test DBs via EnsureCreated) handles this automatically — no USING needed.

**Legacy coordinate data policy:** legacy pre-mm values must be corrected via EF migration (`20260301103000_ScaleLegacySectorDataToMillimetres`) and NOT via runtime spawn/request paths.

**Overlap / distance math — use `double` not `float`:**
When computing squared distances between two `long` coordinates (e.g. in `SpawnController` reposition logic), cast to `double` via `Math.Pow`. Do NOT use `MathF.Pow` — float loses precision on large mm values.

---

## Architecture Rules — Never Violate

- **Server-authoritative physics** — client NEVER submits positions; only input events
- **`X-Shard-Secret` header** — ALL shard-to-shard HTTP calls must attach it; all shard HTTP pipelines must validate it. **Naming:** `SHARD_SECRET` = env var/K8s Secret key; `X-Shard-Secret` = HTTP header name. These are intentionally different — do not conflate.
- **SignalR/REST split** — no FR is served by both; SignalR = game state + player input; REST = auth, marketplace, loadout, admin, catalogue reads
- **No shared in-process state across shards** — each shard pod owns its region exclusively
- **JWT auth flow** — token passed as `?access_token=...` query param on SignalR WebSocket upgrade (browser limitation)
- **No migrations in scaffold stories** — `AppDbContext` has no `DbSet<>` until the story that first needs that table; run `dotnet ef migrations add` only then
- **`BelterLife.Shared` has no project references** — it is referenced by others; never create circular deps

---

## Toolchain Gotchas (learned the hard way)

### .NET 10 — Solution File Format

.NET 10 `dotnet new sln` creates **`BelterLife.slnx`** (new XML-based format), **NOT** `BelterLife.sln`.

Always use:
```bash
dotnet build server/BelterLife.slnx
dotnet test server/BelterLife.slnx
dotnet restore server/BelterLife.slnx
```

CI pipelines, README, and any scripts must reference `.slnx`.  
*Discovered: Story 1.1*

---

### SignalR MessagePack — Correct NuGet Package

`Microsoft.AspNetCore.SignalR` is a **framework package** (included in the ASP.NET Core SDK). Adding it explicitly produces NU1510 warning and should be removed.

To enable MessagePack protocol on the Gateway, use:
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" />
```

Then in `Program.cs`:
```csharp
builder.Services.AddSignalR().AddMessagePackProtocol();
```

Do **NOT** add `Microsoft.AspNetCore.SignalR` as an explicit package reference.  
*Discovered: Story 1.1*

---

### EF Core — InMemory Provider Divergence (hits every DB story)

The InMemory provider behaves differently from production Postgres in three important ways:

**1. No transaction support** — wrap EF operations in a `ProviderName` guard:
```csharp
if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
{
    await using var tx = await db.Database.BeginTransactionAsync();
    // ... operations ...
    await tx.CommitAsync();
}
```

**2. `IsRelational()` throws on InMemory** — never use `db.Database.IsRelational()` in code paths that run in tests. Use `ProviderName` check instead.

**3. `WebApplicationFactory` integration tests need `UseInternalServiceProvider`** to avoid "multiple providers" error:
```csharp
options.UseInMemoryDatabase("TestDb")
    .UseInternalServiceProvider(
        new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider());
```

*Discovered: Stories 1.2, 1.3 — applies to every story that touches EF Core in tests.*

---

### PixiJS v8 — API Changes from v7

Training data contains v7 patterns. Key v8 differences:

| v7 (wrong) | v8 (correct) |
|---|---|
| `new Application(); app.init(...)` (sync) | `const app = new Application(); await app.init(...)` (async) |
| `sprite.cacheAsBitmap = true` | `container.cacheAsTexture(true)` |
| `new Graphics().beginFill(0xff0000)` | `new Graphics().fill(0xff0000)` (method chaining, new API) |

**`Application.init()` is async in v8** — call it with `await` inside an `async` function. Never call synchronously.

**`cacheAsTexture(true)`** is called on a `Container` instance, not a property assignment.

*Discovered: Story 1.4 — applies to all PixiJS rendering stories.*

---

### TypeScript — `noUnusedLocals: true` in `client/tsconfig.json`

The Vite tsconfig has `noUnusedLocals: true` and `noUnusedParameters: true`. This means:

- Stub class fields that are declared but never read will **fail the build** (TS6133)
- Pattern to handle stub fields: expose them via a public getter or method:

```typescript
// ❌ FAILS — TS6133: '_connection' is declared but never read
private _connection = buildConnection();

// ✅ WORKS — field is read by the getter
private connection: HubConnection;
constructor() { this.connection = buildConnection(); }
getConnection(): HubConnection { return this.connection; }
```

*Discovered: Story 1.1*

---

### Vite Scaffold — Interactive Prompt

`npm create vite@latest` prompts for `y` to install `create-vite` if not cached. To avoid hanging:

```bash
npm install -g create-vite
npx create-vite client --template vanilla-ts
```

*Discovered: Story 1.1*

---

## Key File Locations

| What | Where |
|---|---|
| Solution file | `server/BelterLife.slnx` |
| AppDbContext (snake_case naming) | `server/BelterLife.Simulation/Infrastructure/AppDbContext.cs` |
| SignalR hub | `server/BelterLife.Gateway/Hubs/GameHub.cs` |
| Gateway entry point | `server/BelterLife.Gateway/Program.cs` |
| Simulation entry point | `server/BelterLife.Simulation/Program.cs` |
| Client entry point | `client/src/main.ts` → `app.ts` |
| Client world state | `client/src/state/WorldState.ts` |
| Client SignalR client | `client/src/network/GameHubClient.ts` |
| Client TypeScript config | `client/tsconfig.json` |
| Local dev environment | `docker-compose.yml` + `.env` (copy from `.env.example`) |
| Sprint status | `_bmad-output/implementation-artifacts/sprint-status.yaml` |
| Architecture spec | `_bmad-output/planning-artifacts/architecture.md` |
| Epics and stories | `_bmad-output/planning-artifacts/epics.md` |

---

## Story Implementation Log

| Story | Key Finding |
|---|---|
| 2.0 Coordinate system | All entity X/Y are `long` (int64 mm). PhysicsEngine position integration uses `(long)` cast. float→bigint EF migration needs manual `USING x::bigint` SQL for live DBs. Overlap checks use `double`/`Math.Pow`, not float. `RegionBounds` is canonical for sector size — never hardcode. |
| 1.1 Monorepo scaffold | `.slnx` not `.sln`; SignalR Protocols.MessagePack package; tsconfig noUnusedLocals |
| 1.2 Player auth | `erasableSyntaxOnly` bans TS constructor parameter properties; `CanReadToken()` ≠ safe to parse (wrap ReadJwtToken in try/catch); UserManager test factory needs UserValidator + relaxed IdentityOptions; `dotnet-ef` global tool must be on PATH; `WebApplicationFactory` config overrides via `ConfigureAppConfiguration` arrive too late — use `Environment.SetEnvironmentVariable` in factory constructor; EF Core 10 `IsRelational()` throws on InMemory — check `ProviderName != "Microsoft.EntityFrameworkCore.InMemory"` instead; EF Core "multiple providers" error with `UseInMemoryDatabase` in integration tests — fix: `UseInternalServiceProvider(new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider())` |
| 1.3 Starting sector | `Forbid()` without auth middleware = 500; use `StatusCode(403)`. `IShardClient` interface required for Moq. `IDbContextTransaction` requires `ProviderName` guard for InMemory. `AddHttpClient<ShardClient>().AddTypedClient<IShardClient, ShardClient>()` registers both concrete + interface. |
| 1.4 SignalR real-time | `replace_string_in_file` can duplicate a method body if context is ambiguous — verify result. `SendAsync` is an extension method, cannot be Moq'd — use `SendCoreAsync`. `IServiceScopeFactory` requires `using Microsoft.Extensions.DependencyInjection`. `AsNoTracking()` on Ships prevents EF from tracking mutations for `SaveChangesAsync` — remove it from the query used by the physics loop. Gateway/Program.cs still uses old InMemory check — do NOT replicate; use Environment check. |
| 1.5 Ship flight | `InputManager.ts` constructor parameter shorthand (`private hubClient`) banned by `erasableSyntaxOnly` — use explicit field + assignment. `AsNoTracking()` removed from Ships in SimulationLoop so EF tracks mutations. |
| 1.6 Touch input | `e.preventDefault()` in `touchmove` must be inside identifier-match guard — otherwise blocks all concurrent finger scrolling. Joystick DOM mount + event listeners should be guarded by `navigator.maxTouchPoints > 0` — joystick should not appear on desktop. Use dominant-axis output (zero minor axis when orthogonal axis is larger) to prevent diagonal-drag simultaneity bugs. |
| 1.7 Session persistence | `SpawnController` was returning `(0, 0)` for returning players since Story 1.3 — latent bug surfaced here. Reset `Ship.Heading` to 0 on reposition (not just position). Toast/notification overlays need `pointer-events: none` to avoid blocking canvas interaction. |
| 1.4b Login/register screen | During startup, `spawn()` must use a 5-second timeout (AbortController) and surface a retry-friendly failure dialog when the gateway is unavailable. Set page background to match Pixi canvas (`#0a0a1a`) in `index.html` to prevent white flash before canvas init; show a minimal “Connecting…” overlay while bootstrap is pending. |
