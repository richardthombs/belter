# Story 1.3: Procedurally Generated Starting Sector

Status: done

## Story

As a **new player**,
I want to enter a starting sector populated with procedurally generated asteroids when I first log in,
So that there is a live game world to explore from my very first session.

## Acceptance Criteria

1. **Given** a new player account, **When** `POST /api/v1/players/me/spawn` is called with a valid JWT, **Then** a sector is assigned and a ship entity is created at spawn point (0, 0 sector-local); the response includes `sectorId`, `shipId`, `spawnX`, `spawnY`; repeated calls are idempotent (return existing sector/ship without creating duplicates)

2. **Given** a generated sector, **When** created, **Then** it contains 20–50 asteroids with procedurally varied positions, shapes (`vertexCount` 6–12), and radii (5–40 units); no two sectors produced by different seeds are identical

3. **Given** the starting sector, **When** populated, **Then** at least one `NpcStation` record exists within 600 units of the spawn point (0, 0)

4. **Given** the EF Core schema, **When** migrations run, **Then** `sectors`, `asteroids`, `ships`, `players` (game state), and `npc_stations` tables exist with `snake_case` column names (EFCore.NamingConventions)

## Tasks / Subtasks

- [x] Task 1 — Fill in entity models in `BelterLife.Shared/Entities/` and add new entities (AC: 1, 2, 3, 4)
  - [x] Update `server/BelterLife.Shared/Entities/Player.cs` — game state fields: `Id` (string PK = IdentityUser.Id), `SectorId` (int), `ShipId` (int), `LastSeenAt` (DateTimeOffset)
  - [x] Update `server/BelterLife.Shared/Entities/Asteroid.cs`: `Id` (int PK), `SectorId` (int FK), `X`/`Y` (float), `Radius` (float), `VertexCount` (int), `RotationOffset` (float)
  - [x] Update `server/BelterLife.Shared/Entities/Ship.cs`: `Id` (int PK), `PlayerId` (string FK), `SectorId` (int FK), `X`/`Y` (float), `VelocityX`/`VelocityY` (float, default 0), `Heading` (float, default 0)
  - [x] Create `server/BelterLife.Shared/Entities/Sector.cs`: `Id` (int PK), `Seed` (long), `CreatedAt` (DateTimeOffset)
  - [x] Create `server/BelterLife.Shared/Entities/NpcStation.cs`: `Id` (int PK), `SectorId` (int FK), `X`/`Y` (float), `Name` (string)
  - [x] Create `server/BelterLife.Shared/Contracts/Api/SpawnRequest.cs`: `record SpawnRequest(string PlayerId)`
  - [x] Create `server/BelterLife.Shared/Contracts/Api/SpawnResponse.cs`: `record SpawnResponse(int SectorId, int ShipId, float SpawnX, float SpawnY)`

- [x] Task 2 — Configure `AppDbContext` and run first EF Core migration for game schema (AC: 4)
  - [x] Add `DbSet<Sector>`, `DbSet<Asteroid>`, `DbSet<Ship>`, `DbSet<Player>`, `DbSet<NpcStation>` to `AppDbContext.cs`
  - [x] Configure Fluent API in `OnModelCreating` (call `base.OnModelCreating` first): PKs, FKs, max lengths for strings, indexes on `SectorId`
  - [x] Add `Microsoft.EntityFrameworkCore.Design` (10.0.3, `PrivateAssets=all`) to `BelterLife.Simulation.csproj`
  - [x] Create `server/BelterLife.Simulation/Infrastructure/AppDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<AppDbContext>` — same pattern as Story 1.2's `GatewayDbContextFactory`
  - [x] Run migration: `export PATH="$PATH:/Users/richardthombs/.dotnet/tools" && dotnet ef migrations add InitialGameSchema --project server/BelterLife.Simulation --startup-project server/BelterLife.Simulation`
  - [x] Verify generated migration SQL creates: `sectors`, `asteroids`, `ships`, `players`, `npc_stations` tables with `snake_case` columns

- [x] Task 3 — Change Simulation to ASP.NET Core Web hosting (enables internal HTTP endpoints) (AC: 1)
  - [x] Change `BelterLife.Simulation.csproj` SDK from `Microsoft.NET.Sdk.Worker` to `Microsoft.NET.Sdk.Web`
  - [x] Update `server/BelterLife.Simulation/Program.cs`: replace `Host.CreateApplicationBuilder(args)` with `WebApplication.CreateBuilder(args)`, add `builder.Services.AddControllers()`, add `app.MapControllers()` in middleware
  - [x] Keep `SimulationLoop` `AddHostedService` and `AppDbContext` `AddDbContext` registrations unchanged
  - [x] Add auto-migration at startup using `ProviderName` guard (NOT `IsRelational()` — see Dev Notes)
  - [x] Create `server/BelterLife.Simulation/appsettings.json` with Logging and AllowedHosts sections
  - [x] Add `ASPNETCORE_URLS: "http://0.0.0.0:5001"` to `shard` service in `docker-compose.yml`

- [x] Task 4 — Implement `SectorGenerator` service in Simulation (AC: 2, 3)
  - [x] Create `server/BelterLife.Simulation/Entities/SectorGenerator.cs` as a stateless service
  - [x] `long NewSeed()` — returns `new Random().NextInt64()`
  - [x] `(Sector sector, List<Asteroid> asteroids, List<NpcStation> stations) Generate(long seed)`:
    - Create `Sector { Seed = seed, CreatedAt = DateTimeOffset.UtcNow }`
    - `var rng = new Random((int)(seed ^ seed >> 32))` for seeded randomness
    - Asteroid count: `rng.Next(20, 51)` 
    - Per asteroid: polar coords `angle = rng.NextDouble() * Math.PI * 2`, `dist = 150 + rng.NextDouble() * 750` (150–900 units; safe zone 0–100 is excluded), `X = cos(angle)*dist`, `Y = sin(angle)*dist`, `Radius = 5f + rng.NextDouble() * 35f`, `VertexCount = rng.Next(6, 13)`, `RotationOffset = rng.NextDouble() * Math.PI * 2`
    - NPC station: `angle = rng.NextDouble() * Math.PI * 2`, `dist = 200 + rng.NextDouble() * 400` (200–600 units), `Name = "Station Alpha"` (single station for MVP)
  - [x] Register `SectorGenerator` as `AddSingleton<SectorGenerator>()` in Program.cs

- [x] Task 5 — Implement `SpawnController` on Simulation shard (AC: 1, 2, 3, 4)
  - [x] Create `server/BelterLife.Simulation/Api/SpawnController.cs` with `[ApiController]`, `[Route("api/internal")]`
  - [x] `[HttpPost("spawn")]` — accept `[FromBody] SpawnRequest request`
  - [x] Validate `X-Shard-Secret` header matches `SHARD_SECRET` config value; return `Forbid()` on mismatch
  - [x] Check idempotency: if `Player` record with `request.PlayerId` already exists in DB, return `Ok(existing SpawnResponse)` immediately
  - [x] Call `SectorGenerator.Generate(sectorGenerator.NewSeed())` to produce sector data
  - [x] Persist `Sector`, all `Asteroid` records, `NpcStation` record in one `SaveChangesAsync()` call; use generated `Sector.Id`
  - [x] Create `Ship { PlayerId = request.PlayerId, SectorId = sector.Id, X = 0, Y = 0, VelocityX = 0, VelocityY = 0, Heading = 0 }` 
  - [x] Create `Player { Id = request.PlayerId, SectorId = sector.Id, ShipId = ship.Id, LastSeenAt = DateTimeOffset.UtcNow }`
  - [x] Return `StatusCode(201, new SpawnResponse(sector.Id, ship.Id, 0f, 0f))`

- [x] Task 6 — Implement `ShardClient` + `PlayersController` on Gateway (AC: 1)
  - [x] Create `server/BelterLife.Gateway/Infrastructure/ShardClient.cs` (typed HttpClient):
    - Constructor: `HttpClient http` + `IConfiguration config`
    - Reads `Shard__BaseUrl` (or falls back to `http://shard:5001`) and `SHARD_SECRET`
    - `Task<SpawnResponse?> SpawnAsync(string playerId)` — POST to `/api/internal/spawn`, adds `X-Shard-Secret` header, deserializes response
  - [x] Register in `Program.cs`: `builder.Services.AddHttpClient<ShardClient>(c => c.BaseAddress = new Uri(builder.Configuration["Shard__BaseUrl"] ?? "http://shard:5001"))`
  - [x] Create `server/BelterLife.Gateway/Api/v1/PlayersController.cs` with `[ApiController]`, `[Route("api/v1/players")]`, `[Authorize]`
  - [x] `[HttpPost("me/spawn")]` — extract `userId` from `User.FindFirstValue(JwtRegisteredClaimNames.Sub)!`, call `shardClient.SpawnAsync(userId)`, return `Ok(response)`

- [x] Task 7 — Write tests (AC: 1, 2, 3, 4)
  - [x] Add to `BelterLife.Simulation.Tests.csproj`: `Microsoft.EntityFrameworkCore.InMemory` (10.0.3), `Microsoft.AspNetCore.Mvc.Testing` (10.0.3), `Moq` (4.20.72)
  - [x] Add `InternalsVisibleTo` to `BelterLife.Simulation.csproj` for test project
  - [x] Create `server/BelterLife.Simulation.Tests/Entities/SectorGeneratorTests.cs`:
    - `GenerateSector_AsteroidCount_IsBetween20And50()`
    - `GenerateSector_AlwaysHasExactlyOneNpcStation()`
    - `GenerateSector_NpcStation_IsWithin600UnitsOfOrigin()`
    - `GenerateSector_DifferentSeeds_ProduceDifferentLayouts()`
    - `GenerateSector_NoAsteroid_IsWithin100UnitsOfOrigin()` (safe spawn zone)
  - [x] Create `server/BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs` (using InMemory AppDbContext):
    - `Spawn_NewPlayer_Creates201WithSpawnResponse()`
    - `Spawn_ExistingPlayer_Returns200WithSameIds()` (idempotent)
    - `Spawn_MissingShardSecret_Returns403()`
  - [x] Create `server/BelterLife.Gateway.Tests/Api/PlayersControllerTests.cs`:
    - `Spawn_AuthenticatedUser_CallsShardAndReturnsOk()` (mock ShardClient)
    - `Spawn_UnauthenticatedUser_Returns401()`

- [x] Task 8 — End-to-end verification (AC: 1, 2, 3, 4)
  - [x] `dotnet build server/BelterLife.slnx` → 0 errors
  - [x] `dotnet test server/BelterLife.slnx` → all tests passing
  - [x] `cd client && npm run build` → 0 TypeScript errors
  - [x] Inspect generated migration SQL: verify `snake_case` table and column names

## Dev Notes

### Architecture Guardrails — MUST Follow

- **`AppDbContext` has NO `DbSet<>` properties today** — Task 2 adds the first ones. Do NOT add EF Core data annotations (`[Key]`, `[Column]`, `[ForeignKey]`) to `BelterLife.Shared/Entities/` — Shared has no EF Core packages. Use Fluent API exclusively in `AppDbContext.OnModelCreating`.
- **`UseSnakeCaseNamingConvention()` is ALREADY wired** in `Simulation/Program.cs` via `AddDbContext`. Never add it again, never override it. This is what produces `snake_case` column names. Project-context.md: "Never remove or override it."
- **Migrations live in `server/BelterLife.Simulation/Migrations/`** (EF Core default output) — this is the FIRST AppDbContext migration. Simulation owns and applies AppDbContext migrations. Do NOT create AppDbContext migrations in any other project.
- **ProviderName guard for migrations** — learned in Story 1.2: `db.Database.IsRelational()` throws on InMemory in EF Core 10. Use `db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory"` instead.
- **`Player` game record ≠ `IdentityUser`** — `Player.Id` equals the `IdentityUser.Id` string (GUID), but they live in separate tables (`players` vs `asp_net_users`). `GatewayDbContext` owns `asp_net_users`; `AppDbContext` owns `players`. Same PostgreSQL DB, separate contexts, separate table sets — no conflict.
- **X-Shard-Secret on ALL internal shard endpoints** — `SHARD_SECRET` env var = K8s Secret key; `X-Shard-Secret` = HTTP header name. Reject requests without matching header with 403. This applies to every `api/internal/*` route.
- **Simulation SDK change to `Microsoft.NET.Sdk.Web`** — Worker Services can be upgraded to Web hosting; `IHostedService` continues to work unchanged. After the SDK change, Kestrel handles HTTP on port 5001; `SimulationLoop` runs as usual via `IHostedService`.
- **`SimulationLoop` in integration tests** — remove `IHostedService` registration for `SimulationLoop` in test factory `ConfigureServices` to prevent the game loop from running during tests (it will attempt to read from an empty in-memory DB). See pattern in Dev Notes below.
- **SignalR/REST split** — `POST /api/v1/players/me/spawn` is REST-only. Do NOT add spawn logic to `GameHub.cs`. SignalR carries game state; REST handles account/session setup.

### EF Core Migration Setup for Simulation

Add to `BelterLife.Simulation.csproj`:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.3">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

`AppDbContextFactory` (design-time factory — same pattern as Story 1.2's `GatewayDbContextFactory`):
```csharp
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=belterlife;Username=belter;Password=changeme";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
```

Migration command (from repo root):
```bash
export PATH="$PATH:/Users/richardthombs/.dotnet/tools"
dotnet ef migrations add InitialGameSchema \
  --project server/BelterLife.Simulation \
  --startup-project server/BelterLife.Simulation
```

### Simulation SDK Change — Program.cs Pattern

```csharp
// Before (Worker SDK): Host.CreateApplicationBuilder(args)
// After (Web SDK): WebApplication.CreateBuilder(args)

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<SectorGenerator>();
builder.Services.AddHostedService<SimulationLoop>(); // unchanged
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.MapControllers();
app.Run();
```

### Sector Coordinate Space

- Play field: −1000 to +1000 units on both axes (2000×2000 total)
- Spawn point: (0, 0) — center of sector, clear of all objects
- Safe zone: 100-unit radius around origin — NO asteroids
- Asteroid scatter: 150–900 units from origin (polar placement avoids safe zone)
- NPC station: 200–600 units from origin — reachable but not at spawn

Polar placement pattern:
```csharp
var angle = rng.NextDouble() * Math.PI * 2;
var dist = minDist + rng.NextDouble() * (maxDist - minDist);
var x = (float)(Math.Cos(angle) * dist);
var y = (float)(Math.Sin(angle) * dist);
```

### AppDbContext Fluent API Pattern

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Sector>(e =>
    {
        e.HasKey(s => s.Id);
    });

    modelBuilder.Entity<Asteroid>(e =>
    {
        e.HasKey(a => a.Id);
        e.HasOne<Sector>().WithMany().HasForeignKey(a => a.SectorId);
        e.HasIndex(a => a.SectorId);
    });

    modelBuilder.Entity<Ship>(e =>
    {
        e.HasKey(s => s.Id);
        e.Property(s => s.PlayerId).HasMaxLength(450); // IdentityUser Id length
        e.HasIndex(s => s.PlayerId).IsUnique(); // one ship per player for now
    });

    modelBuilder.Entity<Player>(e =>
    {
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasMaxLength(450);
    });

    modelBuilder.Entity<NpcStation>(e =>
    {
        e.HasKey(n => n.Id);
        e.Property(n => n.Name).HasMaxLength(100);
        e.HasIndex(n => n.SectorId);
    });
}
```

### ShardClient Configuration

```csharp
// Gateway Program.cs
builder.Services.AddHttpClient<ShardClient>(c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Shard__BaseUrl"] ?? "http://shard:5001");
});

// ShardClient.cs usage
var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/spawn")
{
    Content = JsonContent.Create(new SpawnRequest(playerId)),
};
req.Headers.Add("X-Shard-Secret", shardSecret);
```

### Integration Test Factory for Simulation

For `SpawnControllerTests.cs` using `WebApplicationFactory<Program>`:

```csharp
public class SimulationWebApplicationFactory : WebApplicationFactory<Program>
{
    readonly string dbName = "SimulationTest_" + Guid.NewGuid();

    public SimulationWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Host=placeholder");
        Environment.SetEnvironmentVariable("SHARD_SECRET", "test-shard-secret");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove SimulationLoop — prevents game loop running in tests
            var loopDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(SimulationLoop));
            if (loopDescriptor != null) services.Remove(loopDescriptor);

            // Replace PostgreSQL AppDbContext with InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase(dbName)
                   .UseInternalServiceProvider(inMemoryProvider));
        });
    }
}
```

Key: `UseInternalServiceProvider` bypasses the "multiple providers" EF Core check (learned in Story 1.2). `SimulationLoop` removal prevents test hangs.

### NuGet Packages Summary

**BelterLife.Simulation.csproj — add:**
- `Microsoft.EntityFrameworkCore.Design` (10.0.3, PrivateAssets=all)

**SDK change:** `Microsoft.NET.Sdk.Worker` → `Microsoft.NET.Sdk.Web` (no new packages needed; ASP.NET Core is included)

**BelterLife.Simulation.Tests.csproj — add:**
- `Microsoft.EntityFrameworkCore.InMemory` (10.0.3)
- `Microsoft.AspNetCore.Mvc.Testing` (10.0.3)
- `Moq` (4.20.72)

**BelterLife.Gateway.csproj — no new packages needed:** `IHttpClientFactory` is in `Microsoft.Extensions.Http` which is included in the Web SDK.

### docker-compose Changes

Under `shard` service `environment`:
```yaml
ASPNETCORE_URLS: "http://0.0.0.0:5001"
```
(The port `5001` mapping already exists; this makes Kestrel bind to it after the SDK change.)

### References

- Story ACs: [Source: epics.md#Story 1.3: Procedurally Generated Starting Sector]
- Entity file locations: [Source: architecture.md#BelterLife.Shared/Entities/]
- AsteroidManager, ShipManager, SectorGenerator placement: [Source: architecture.md#BelterLife.Simulation/Entities/]
- AppDbContext snake_case: [Source: project-context.md#Critical Naming Conventions, architecture.md#Database naming]
- X-Shard-Secret: [Source: project-context.md#Architecture Rules — Never Violate]
- Simulation file structure: [Source: architecture.md#BelterLife.Simulation/]
- Server-authoritative physics NFR12: [Source: architecture.md#NFR Coverage]
- World & Belt FR7: [Source: epics.md#FR7]
- SpawnController / internal HTTP: [Source: architecture.md#Architectural Boundaries — Internal boundary]
- Gateway Routing/ShardClient: [Source: architecture.md#BelterLife.Gateway/Routing/]
- Story 1.2 EF Core gotchas (ProviderName, UseInternalServiceProvider): [Source: project-context.md#Story Implementation Log]
- dotnet-ef PATH: [Source: project-context.md#Toolchain Gotchas]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-5 (via GitHub Copilot)
code-review: Claude Sonnet 4.6 (via GitHub Copilot)

### Debug Log References

- `Forbid()` without an auth scheme causes 500 in Simulation (no auth middleware) — replaced with `StatusCode(403)`
- `IHostedService` requires `using Microsoft.Extensions.Hosting` in Simulation.Tests
- ShardClient needs `IShardClient` interface for Moq-based unit testing in Gateway.Tests
- `AddHttpClient<ShardClient>().AddTypedClient<IShardClient, ShardClient>()` registers both concrete and interface
- Transaction guard required: `IDbContextTransaction?` using ProviderName check (same as migration guard) — InMemory does not support real transactions
- `IDbContextTransaction` requires `using Microsoft.EntityFrameworkCore.Storage`

### Completion Notes List

- Task 1: Updated Player/Asteroid/Ship entities; created Sector, NpcStation, SpawnRequest, SpawnResponse
- Task 2: AppDbContext configured with all DbSets + Fluent API; EFCore.Design added; AppDbContextFactory created; InitialGameSchema migration generated with snake_case tables (sectors, asteroids, ships, players, npc_stations)
- Task 3: Simulation SDK changed to Web; Program.cs converted to WebApplication pattern with ProviderName guard for migrations; ASPNETCORE_URLS added to docker-compose
- Task 4: SectorGenerator implemented with polar coordinate placement (150–900 asteroid dist, 200–600 station dist, safe zone 100+ units from origin)
- Task 5: SpawnController with X-Shard-Secret validation, idempotency check, full entity persistence
- Task 6: IShardClient interface + ShardClient typed HttpClient; PlayersController with [Authorize] + sub claim extraction
- Task 7: 5 SectorGenerator unit tests + 3 SpawnController integration tests + 2 PlayersController tests — all 25 total passing
- Task 8: `dotnet build` → 0 errors; `dotnet test` → 25/25 pass; `npm run build` → 0 TypeScript errors; migration SQL verified snake_case
- Code review H1: SpawnController wrapped in IDbContextTransaction (ProviderName guard for InMemory); reduced from 4 to 3 SaveChangesAsync calls with full rollback on failure
- Code review H3: PlayersController.Spawn() now returns 502 on null response or HttpRequestException from ShardClient
- Code review M1: SectorGeneratorTests safe zone assertion corrected from >= 100 to >= 150 (matches actual placement minimum)
- Code review M2: ShardClient now injects ILogger<ShardClient> and logs a warning with status code before re-throwing on non-2xx responses
- Code review L1: Story Dev Notes corrected migration path from Infrastructure/Migrations/ to Migrations/ (EF Core default)

### File List

- server/BelterLife.Shared/Entities/Player.cs (modified)
- server/BelterLife.Shared/Entities/Asteroid.cs (modified)
- server/BelterLife.Shared/Entities/Ship.cs (modified)
- server/BelterLife.Shared/Entities/Sector.cs (created)
- server/BelterLife.Shared/Entities/NpcStation.cs (created)
- server/BelterLife.Shared/Contracts/Api/SpawnRequest.cs (created)
- server/BelterLife.Shared/Contracts/Api/SpawnResponse.cs (created)
- server/BelterLife.Simulation/Infrastructure/AppDbContext.cs (modified)
- server/BelterLife.Simulation/Infrastructure/AppDbContextFactory.cs (created)
- server/BelterLife.Simulation/BelterLife.Simulation.csproj (modified — SDK Worker→Web, EFCore.Design, InternalsVisibleTo)
- server/BelterLife.Simulation/Program.cs (modified — WebApplication, SectorGenerator, Controllers, migration)
- server/BelterLife.Simulation/appsettings.json (modified — added AllowedHosts)
- server/BelterLife.Simulation/Entities/SectorGenerator.cs (created)
- server/BelterLife.Simulation/Api/SpawnController.cs (created)
- server/BelterLife.Simulation/Migrations/20260221204547_InitialGameSchema.cs (created)
- server/BelterLife.Simulation/Migrations/20260221204547_InitialGameSchema.Designer.cs (created)
- server/BelterLife.Simulation/Migrations/AppDbContextModelSnapshot.cs (created)
- server/BelterLife.Gateway/Infrastructure/IShardClient.cs (created)
- server/BelterLife.Gateway/Infrastructure/ShardClient.cs (created)
- server/BelterLife.Gateway/Api/v1/PlayersController.cs (modified)
- server/BelterLife.Gateway/Program.cs (modified — AddHttpClient<ShardClient>)
- server/BelterLife.Simulation.Tests/BelterLife.Simulation.Tests.csproj (modified — InMemory, Mvc.Testing, Moq)
- server/BelterLife.Simulation.Tests/Entities/SectorGeneratorTests.cs (created)
- server/BelterLife.Simulation.Tests/Api/SpawnControllerTests.cs (created)
- server/BelterLife.Gateway.Tests/Api/PlayersControllerTests.cs (created)
- docker-compose.yml (modified — ASPNETCORE_URLS for shard)

**Code Review Fixes:**
- server/BelterLife.Simulation/Api/SpawnController.cs (modified — transaction wrap with ProviderName guard)
- server/BelterLife.Gateway/Api/v1/PlayersController.cs (modified — 502 on null/HttpRequestException)
- server/BelterLife.Simulation.Tests/Entities/SectorGeneratorTests.cs (modified — safe zone assertion >= 150)
- server/BelterLife.Gateway/Infrastructure/ShardClient.cs (modified — ILogger, log warning on non-2xx)
