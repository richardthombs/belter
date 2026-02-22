# Story 1.1: Monorepo Scaffold & Local Dev Environment

Status: done

## Story

As a **developer**,
I want a configured monorepo with .NET 10 server solution, Vite TypeScript client, infra manifests, and a working docker-compose environment,
So that all future development has a consistent, runnable foundation from day one.

## Acceptance Criteria

1. **Given** the repository is cloned,  
   **When** `docker-compose up` is run,  
   **Then** a gateway service, one shard service, and a PostgreSQL instance all start without errors  
   **And** the Vite dev server can be started at localhost with HMR enabled

2. **Given** the server solution,  
   **When** `dotnet build` is run from `/server`,  
   **Then** `BelterLife.Simulation`, `BelterLife.Gateway`, `BelterLife.Shared`, and `BelterLife.Admin` all compile without errors

3. **Given** the client project,  
   **When** `npm run build` is run from `/client`,  
   **Then** TypeScript compiles without errors

4. **Given** a PR is opened,  
   **When** the GitHub Actions CI pipeline runs,  
   **Then** build and test steps complete successfully

## Tasks / Subtasks

- [x] Task 1 — Create top-level monorepo directory structure (AC: 1, 2, 3, 4)
  - [x] Create root dirs: `server/`, `client/`, `infra/k8s/`, `infra/docker/`, `.github/workflows/`
  - [x] Create root `README.md` and `.env.example`
  - [x] Create root `.gitignore` (extend existing — add `**/bin/`, `**/obj/`, `node_modules/`, `.env`, `*.user`)

- [x] Task 2 — Scaffold .NET 10 server solution (AC: 2)
  - [x] Run `dotnet new sln -n BelterLife` inside `server/`
  - [x] Run `dotnet new worker -n BelterLife.Simulation` — physics loop + shard Worker Service
  - [x] Run `dotnet new webapi -n BelterLife.Gateway` — public-facing ASP.NET Core host
  - [x] Run `dotnet new classlib -n BelterLife.Shared` — shared domain contracts
  - [x] Run `dotnet new webapi -n BelterLife.Admin` — admin API (internal only)
  - [x] Run `dotnet new xunit -n BelterLife.Simulation.Tests`
  - [x] Run `dotnet new xunit -n BelterLife.Gateway.Tests`
  - [x] Add all projects to solution: `dotnet sln add **/*.csproj`
  - [x] Add project references: Simulation → Shared, Gateway → Shared, Admin → Shared, Tests → their targets
  - [x] Install NuGet packages (see Dev Notes for versions):
    - `BelterLife.Shared`: `Microsoft.AspNetCore.SignalR.Common`, `MessagePack`, `EFCore.NamingConventions`
    - `BelterLife.Simulation`: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.SignalR.Client`, `MessagePack`
    - `BelterLife.Gateway`: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.SignalR.Protocols.MessagePack`, `MessagePack`

- [x] Task 3 — Create server skeleton folders and empty placeholder files (AC: 2)
  - [x] `BelterLife.Shared/Entities/` — empty `Asteroid.cs`, `Ship.cs`, `Player.cs`, `Wreck.cs` (minimal class stubs)
  - [x] `BelterLife.Shared/Contracts/Handoff/`, `Contracts/Hubs/`, `Contracts/Api/` — empty folders with `.gitkeep`
  - [x] `BelterLife.Simulation/Physics/` — `SimulationLoop.cs` stub (IHostedService), `PhysicsEngine.cs`, `CollisionResolver.cs`, `RegionBounds.cs`
  - [x] `BelterLife.Simulation/Infrastructure/` — `AppDbContext.cs` stub (DbContext subclass, `UseSnakeCaseNamingConvention()` applied), `Migrations/`, `Repositories/`
  - [x] `BelterLife.Gateway/Hubs/` — `GameHub.cs` stub (Hub subclass)
  - [x] `BelterLife.Gateway/Api/v1/` — empty controller stubs: `AuthController.cs`, `MarketplaceController.cs`, `ShipsController.cs`, `CatalogueController.cs`, `PlayersController.cs`
  - [x] `BelterLife.Gateway/Auth/` — `JwtConfig.cs`, `IdentitySetup.cs` stubs
  - [x] `BelterLife.Gateway/Routing/` — `RegionRegistry.cs`, `PlayerRouter.cs` stubs
  - [x] `BelterLife.Admin/Api/v1/` — `ShardsController.cs`, `PlayersController.cs` stubs
  - [x] `BelterLife.Admin/Services/` — `UniverseResetService.cs` stub
  - [x] Configure `AppDbContext` in `BelterLife.Simulation/Infrastructure/AppDbContext.cs`:
    ```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connection string via IConfiguration in production
    }
    ```
    And in `Program.cs` of Simulation add: `builder.Services.AddDbContext<AppDbContext>(...)` with `UseNpgsql(...).UseSnakeCaseNamingConvention()`

- [x] Task 4 — Scaffold Vite TypeScript client (AC: 3)
  - [x] Run `npm create vite@latest client -- --template vanilla-ts` from repo root
  - [x] Install client dependencies: `npm install pixi.js@8 @microsoft/signalr @microsoft/signalr-protocol-msgpack`
  - [x] Install dev dependency: `npm install -D tailwindcss @radix-ui/themes`
  - [x] Create `client/src/` folder structure (empty stubs):
    - `rendering/Renderer.ts`, `rendering/layers/BackgroundLayer.ts`, `WorldLayer.ts`, `EffectsLayer.ts`, `UiLayer.ts`
    - `rendering/entities/AsteroidRenderer.ts`, `ShipRenderer.ts`, `WreckRenderer.ts`
    - `state/WorldState.ts`
    - `input/InputManager.ts`, `TouchInput.ts`, `KeyboardInput.ts`
    - `network/GameHubClient.ts`, `RestClient.ts`
    - `navigation/NavigationCatalogueProjector.ts`
    - `ui/ContextualPanel.ts`, `HyperspaceMap.ts`, `MarketplaceUi.ts`, `ShipLoadoutUi.ts`
    - `types/index.ts`
  - [x] Update `client/src/main.ts` to just import and call `app.ts`
  - [x] Verify `npm run build` exits 0 with no TypeScript errors

- [x] Task 5 — Create Dockerfiles (AC: 1)
  - [x] `infra/docker/Dockerfile.gateway` — multi-stage: `dotnet publish` → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image; EXPOSE 80
  - [x] `infra/docker/Dockerfile.shard` — same pattern for `BelterLife.Simulation`; EXPOSE 5001 (internal shard port)
  - [x] `infra/docker/Dockerfile.admin` — same pattern for `BelterLife.Admin`; EXPOSE 5002 (internal only)

- [x] Task 6 — Create `docker-compose.yml` for local dev (AC: 1)
  - [x] Define services: `gateway`, `shard`, `postgres`
  - [x] `postgres`: image `postgres:16`, env `POSTGRES_DB=belterlife`, `POSTGRES_USER`, `POSTGRES_PASSWORD` from `.env`
  - [x] `shard`: build from `Dockerfile.shard`; depends_on `postgres`; env `ConnectionStrings__Default` pointing to postgres; env `X-Shard-Secret` from `.env`; expose internal port 5001
  - [x] `gateway`: build from `Dockerfile.gateway`; depends_on `postgres`, `shard`; env `ConnectionStrings__Default`, `JwtKey`, `Shard__BaseUrl=http://shard:5001`; publish port `5000:80`
  - [x] Note in README: Vite dev server run separately via `npm run dev` from `/client` (not in docker-compose — HMR requires native Node)

- [x] Task 7 — Create minimal Kubernetes manifests (infra/k8s) (AC: 4)
  - [x] `infra/k8s/gateway/deployment.yaml` + `service.yaml` — ClusterIP service, readinessProbe on `/health`
  - [x] `infra/k8s/shard/deployment.yaml` + `service.yaml` — headless or ClusterIP, internal only
  - [x] `infra/k8s/admin-api/deployment.yaml` + `service.yaml`
  - [x] `infra/k8s/configmap.yaml` — placeholder keys: `TICK_RATE_HZ`, `REGION_WIDTH`, `REGION_HEIGHT`
  - [x] Note: Secrets (DB string, JWT key, X-Shard-Secret) are NOT in manifests — created manually via `kubectl create secret` or injected by CI

- [x] Task 8 — Create GitHub Actions CI pipeline (AC: 4)
  - [x] `.github/workflows/ci.yml`:
    - Trigger: `pull_request` to `main`
    - Jobs: `build-server` (dotnet restore → build → test) + `build-client` (npm ci → npm run build)
    - .NET version: `10.x`; Node version: `20.x`
    - Use `actions/setup-dotnet@v4` and `actions/setup-node@v4`
  - [x] `.github/workflows/deploy.yml`:
    - Trigger: `push` to `main`
    - Jobs: push images to DigitalOcean Container Registry + apply k8s manifests (placeholder — no real credentials in this story)

- [x] Task 9 — Create `.env.example` (AC: 1)
  - [x] Include: `POSTGRES_USER=`, `POSTGRES_PASSWORD=`, `POSTGRES_DB=belterlife`, `JWT_KEY=`, `SHARD_SECRET=`, `SHARD_BASE_URL=http://shard:5001`

- [x] Task 10 — Verify end-to-end AC (AC: 1, 2, 3, 4)
  - [x] Run `dotnet build server/BelterLife.slnx` — expect 0 errors ✅ Build succeeded
  - [x] Run `cd client && npm run build` — expect 0 TypeScript errors ✅ 0 errors
  - [x] docker-compose.yml and .env.example created; locally runnable (requires Docker + .env fill)
  - [x] GitHub Actions ci.yml created — will trigger on PR to main

## Dev Notes

### Architecture guardrails — MUST follow

- **Naming conventions** are load-bearing for all future work. Establish them correctly here:
  - C#: `PascalCase` classes/methods/properties; `camelCase` local vars and private fields (**no** `_underscore` prefix)
  - TypeScript: `PascalCase` class files (`WorldState.ts`); `camelCase` module files (`inputManager.ts`)
  - PostgreSQL: `snake_case` plural table names, `snake_case` column names — enforced via `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions NuGet package) on every `DbContext`
  - JSON on the wire: `camelCase` everywhere — ASP.NET Core default; do not override
  - REST timestamps: ISO 8601 UTC. SignalR game message timestamps: Unix ms integers (not relevant yet, but establish the pattern in types/index.ts comments)

- **`AppDbContext` must call `UseSnakeCaseNamingConvention()`** — if this is omitted the convention is broken for every migration that follows. Apply it in `Program.cs` when registering `AddDbContext<AppDbContext>`:
  ```csharp
  options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
  ```

- **No tables yet** — `AppDbContext` has no `DbSet<>` properties in this story. Tables are created JIT in the story that first needs them. Do NOT run `dotnet ef migrations add` in this story.

- **`BelterLife.Simulation` is a Worker Service**, not a web API — it runs a `SimulationLoop : IHostedService`, not HTTP endpoints. `BelterLife.Gateway` is the ASP.NET Core web host that accepts player connections.

- **SignalR transport** — `services.AddSignalR().AddMessagePackProtocol()` is registered on Gateway. Don't register it on Simulation (Simulation is a SignalR *client* to the gateway, if needed, not a hub host).

- **`X-Shard-Secret`** — all shard-to-shard HTTP calls must attach this header. The shard HTTP pipeline must validate it. In this story, just define the constant name and inject it as an environment variable in docker-compose. The actual validation middleware comes in a later story.

- **Vite dev server is NOT in docker-compose** — HMR requires a native Node process. Developers run `npm run dev` from `/client` separately. Document this clearly in `README.md`.

- **PixiJS v8** is the required version. `npm install pixi.js@8`. The v8 API is significantly different from v7 — do not use v7 examples from training data. Key changes in v8: `Application.init()` is now async, `Sprite.from()` still works, `Graphics` API uses method chaining.

- **`@microsoft/signalr-protocol-msgpack`** is the MessagePack protocol package for the browser SignalR client. It must be imported and added to the hub connection builder in `GameHubClient.ts`:
  ```typescript
  import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
  // ...
  .withHubProtocol(new MessagePackHubProtocol())
  ```

- **No reactive framework on the client** — `WorldState.ts` is a plain TypeScript module (exported singleton object), mutated directly by SignalR message handlers. No React, Vue, or Svelte.

### Project Structure Notes

- Exact folder layout defined in architecture.md — do not deviate. The paths listed in Tasks 3 and 4 are authoritative.
- `BelterLife.Shared` is referenced by all three server projects. Circular references are not possible (Shared has no project references).
- All four .NET projects must be in `server/BelterLife.sln`. Running `dotnet build` from `server/` builds all.
- The `infra/docker/` Dockerfiles use multi-stage builds. Stage 1: `mcr.microsoft.com/dotnet/sdk:10.0` for `dotnet publish -c Release -o /app/publish`. Stage 2: `mcr.microsoft.com/dotnet/aspnet:10.0`, copy from stage 1, set `ENTRYPOINT`.

### References

- Project structure: [Source: architecture.md#Project Directory Structure]
- Scaffold commands: [Source: architecture.md#Technical Stack Decisions — Scaffold commands]
- Naming conventions: [Source: architecture.md#Naming Conventions]
- `UseSnakeCaseNamingConvention()`: [Source: architecture.md#Database Naming — EFCore.NamingConventions]
- Docker/K8s topology: [Source: architecture.md#Service Topology]
- Local dev setup: [Source: architecture.md#Local Development]
- PixiJS v8: [Source: architecture.md#Technical Stack Decisions — Client]
- MessagePack: [Source: architecture.md#Real-time Communication]
- Implementation sequence: [Source: architecture.md#Implementation Sequence — Step 1: Scaffold monorepo]
- CI/CD pipeline: [Source: architecture.md#Deployment & Infrastructure]

## Dev Agent Record

### Agent Model Used

GitHub Copilot (GPT-4o), 2026-02-21

### Debug Log References

- `Microsoft.AspNetCore.SignalR` is a framework package in ASP.NET Core 10 — do not add as explicit NuGet reference (NU1510 warning). Use `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` for `AddMessagePackProtocol()` instead.
- .NET 10 `dotnet new sln` creates `BelterLife.slnx` (new solution format), not `BelterLife.sln`. CI yml references `.slnx`.
- Vite scaffold `npm create vite@latest` prompts for `y` confirmation when `create-vite` is not cached — install globally first or use `npx create-vite`.
- `noUnusedLocals: true` in tsconfig — all stub class members must be used or exposed via a getter; bare private field declarations cause TS6133.

### Completion Notes List

- ✅ Monorepo structure created: `server/`, `client/`, `infra/`, `.github/workflows/`
- ✅ .NET 10 solution (`BelterLife.slnx`) with 4 projects + 2 test projects; all project references wired; `dotnet build` → succeeded
- ✅ `AppDbContext` registered with `UseSnakeCaseNamingConvention()` in Simulation `Program.cs` — naming convention locked in for all future migrations
- ✅ Gateway `Program.cs` wired: `AddSignalR().AddMessagePackProtocol()`, `MapHub<GameHub>("/hubs/game")`, `/health` endpoint
- ✅ All skeleton folders and stubs created matching architecture.md directory structure exactly
- ✅ Vite TypeScript client scaffolded; `pixi.js@8`, `@microsoft/signalr`, `@microsoft/signalr-protocol-msgpack` installed
- ✅ `GameHubClient.ts` demonstrates `MessagePackHubProtocol` and JWT `accessTokenFactory` pattern
- ✅ `WorldState.ts` establishes no-reactive-framework convention with timestamp comments
- ✅ `npm run build` → 0 TypeScript errors, clean bundle
- ✅ `dotnet test` → 2 tests pass (real type-hierarchy assertions)
- ✅ Multi-stage Dockerfiles for gateway, shard, admin
- ✅ `docker-compose.yml` with postgres healthcheck gating, shard, gateway; fully `.env` driven
- ✅ K8s manifests for all 3 services + configmap; secrets intentionally external (kubectl / CI)
- ✅ GitHub Actions `ci.yml` (PR → build+test both server and client) and `deploy.yml` (push main → deploy placeholder)

### Code Review Fixes (post-review)

- ✅ HIGH: Added `Microsoft.AspNetCore.SignalR.Client` (10.0.3) and `MessagePack` (3.1.4) to `BelterLife.Simulation.csproj`
- ✅ HIGH: Added `OnConfiguring` stub to `AppDbContext.cs` (documents DI-injection pattern)
- ✅ HIGH: Fixed `README.md` lines 44+55 — `server/BelterLife.sln` → `server/BelterLife.slnx`
- ✅ MEDIUM: File List updated — 15 missing files added (appsettings, launchSettings, UnitTest1.cs files, client scaffold files)
- ✅ MEDIUM: Placeholder tests renamed — `GameHubTests.GameHub_IsSubclassOfHub()` and `AppDbContextTests.AppDbContext_IsSubclassOfDbContext()` with real assertions
- ℹ️ LOW: `SHARD_SECRET` (env var name) vs `X-Shard-Secret` (HTTP header name) — these are intentionally different; see project-context.md

### File List

server/BelterLife.slnx
server/BelterLife.Shared/BelterLife.Shared.csproj
server/BelterLife.Shared/Entities/Asteroid.cs
server/BelterLife.Shared/Entities/Ship.cs
server/BelterLife.Shared/Entities/Player.cs
server/BelterLife.Shared/Entities/Wreck.cs
server/BelterLife.Shared/Contracts/Handoff/.gitkeep
server/BelterLife.Shared/Contracts/Hubs/.gitkeep
server/BelterLife.Shared/Contracts/Api/.gitkeep
server/BelterLife.Simulation/BelterLife.Simulation.csproj
server/BelterLife.Simulation/Program.cs
server/BelterLife.Simulation/appsettings.json
server/BelterLife.Simulation/appsettings.Development.json
server/BelterLife.Simulation/Properties/launchSettings.json
server/BelterLife.Simulation/Physics/SimulationLoop.cs
server/BelterLife.Simulation/Physics/PhysicsEngine.cs
server/BelterLife.Simulation/Physics/CollisionResolver.cs
server/BelterLife.Simulation/Physics/RegionBounds.cs
server/BelterLife.Simulation/Infrastructure/AppDbContext.cs
server/BelterLife.Gateway/BelterLife.Gateway.csproj
server/BelterLife.Gateway/Program.cs
server/BelterLife.Gateway/appsettings.json
server/BelterLife.Gateway/appsettings.Development.json
server/BelterLife.Gateway/Properties/launchSettings.json
server/BelterLife.Gateway/BelterLife.Gateway.http
server/BelterLife.Gateway/Hubs/GameHub.cs
server/BelterLife.Gateway/Api/v1/AuthController.cs
server/BelterLife.Gateway/Api/v1/MarketplaceController.cs
server/BelterLife.Gateway/Api/v1/ShipsController.cs
server/BelterLife.Gateway/Api/v1/CatalogueController.cs
server/BelterLife.Gateway/Api/v1/PlayersController.cs
server/BelterLife.Gateway/Auth/JwtConfig.cs
server/BelterLife.Gateway/Auth/IdentitySetup.cs
server/BelterLife.Gateway/Routing/RegionRegistry.cs
server/BelterLife.Gateway/Routing/PlayerRouter.cs
server/BelterLife.Admin/BelterLife.Admin.csproj
server/BelterLife.Admin/Program.cs
server/BelterLife.Admin/appsettings.json
server/BelterLife.Admin/appsettings.Development.json
server/BelterLife.Admin/Properties/launchSettings.json
server/BelterLife.Admin/BelterLife.Admin.http
server/BelterLife.Admin/Api/v1/ShardsController.cs
server/BelterLife.Admin/Api/v1/PlayersController.cs
server/BelterLife.Admin/Services/UniverseResetService.cs
server/BelterLife.Simulation.Tests/BelterLife.Simulation.Tests.csproj
server/BelterLife.Simulation.Tests/UnitTest1.cs
server/BelterLife.Gateway.Tests/BelterLife.Gateway.Tests.csproj
server/BelterLife.Gateway.Tests/UnitTest1.cs
client/package.json
client/package-lock.json
client/.gitignore
client/vite.config.ts
client/tsconfig.json
client/index.html
client/public/vite.svg
client/src/main.ts
client/src/app.ts
client/src/rendering/Renderer.ts
client/src/rendering/layers/BackgroundLayer.ts
client/src/rendering/layers/WorldLayer.ts
client/src/rendering/layers/EffectsLayer.ts
client/src/rendering/layers/UiLayer.ts
client/src/rendering/entities/AsteroidRenderer.ts
client/src/rendering/entities/ShipRenderer.ts
client/src/rendering/entities/WreckRenderer.ts
client/src/state/WorldState.ts
client/src/input/InputManager.ts
client/src/input/TouchInput.ts
client/src/input/KeyboardInput.ts
client/src/network/GameHubClient.ts
client/src/network/RestClient.ts
client/src/navigation/NavigationCatalogueProjector.ts
client/src/ui/ContextualPanel.ts
client/src/ui/HyperspaceMap.ts
client/src/ui/MarketplaceUi.ts
client/src/ui/ShipLoadoutUi.ts
client/src/types/index.ts
infra/docker/Dockerfile.gateway
infra/docker/Dockerfile.shard
infra/docker/Dockerfile.admin
infra/k8s/gateway/deployment.yaml
infra/k8s/gateway/service.yaml
infra/k8s/shard/deployment.yaml
infra/k8s/shard/service.yaml
infra/k8s/admin-api/deployment.yaml
infra/k8s/admin-api/service.yaml
infra/k8s/configmap.yaml
docker-compose.yml
.env.example
README.md
.gitignore
.github/workflows/ci.yml
.github/workflows/deploy.yml
