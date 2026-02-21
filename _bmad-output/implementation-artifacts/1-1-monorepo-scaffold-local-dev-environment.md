# Story 1.1: Monorepo Scaffold & Local Dev Environment

Status: ready-for-dev

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

- [ ] Task 1 — Create top-level monorepo directory structure (AC: 1, 2, 3, 4)
  - [ ] Create root dirs: `server/`, `client/`, `infra/k8s/`, `infra/docker/`, `.github/workflows/`
  - [ ] Create root `README.md` and `.env.example`
  - [ ] Create root `.gitignore` (extend existing — add `**/bin/`, `**/obj/`, `node_modules/`, `.env`, `*.user`)

- [ ] Task 2 — Scaffold .NET 10 server solution (AC: 2)
  - [ ] Run `dotnet new sln -n BelterLife` inside `server/`
  - [ ] Run `dotnet new worker -n BelterLife.Simulation` — physics loop + shard Worker Service
  - [ ] Run `dotnet new webapi -n BelterLife.Gateway` — public-facing ASP.NET Core host
  - [ ] Run `dotnet new classlib -n BelterLife.Shared` — shared domain contracts
  - [ ] Run `dotnet new webapi -n BelterLife.Admin` — admin API (internal only)
  - [ ] Run `dotnet new xunit -n BelterLife.Simulation.Tests`
  - [ ] Run `dotnet new xunit -n BelterLife.Gateway.Tests`
  - [ ] Add all projects to solution: `dotnet sln add **/*.csproj`
  - [ ] Add project references: Simulation → Shared, Gateway → Shared, Admin → Shared, Tests → their targets
  - [ ] Install NuGet packages (see Dev Notes for versions):
    - `BelterLife.Shared`: `Microsoft.AspNetCore.SignalR.Common`, `MessagePack`, `EFCore.NamingConventions`
    - `BelterLife.Simulation`: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.SignalR.Client`, `MessagePack`
    - `BelterLife.Gateway`: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.AspNetCore.SignalR`, `MessagePack`

- [ ] Task 3 — Create server skeleton folders and empty placeholder files (AC: 2)
  - [ ] `BelterLife.Shared/Entities/` — empty `Asteroid.cs`, `Ship.cs`, `Player.cs`, `Wreck.cs` (minimal class stubs)
  - [ ] `BelterLife.Shared/Contracts/Handoff/`, `Contracts/Hubs/`, `Contracts/Api/` — empty folders with `.gitkeep`
  - [ ] `BelterLife.Simulation/Physics/` — `SimulationLoop.cs` stub (IHostedService), `PhysicsEngine.cs`, `CollisionResolver.cs`, `RegionBounds.cs`
  - [ ] `BelterLife.Simulation/Infrastructure/` — `AppDbContext.cs` stub (DbContext subclass, `UseSnakeCaseNamingConvention()` applied), `Migrations/`, `Repositories/`
  - [ ] `BelterLife.Gateway/Hubs/` — `GameHub.cs` stub (Hub subclass)
  - [ ] `BelterLife.Gateway/Api/v1/` — empty controller stubs: `AuthController.cs`, `MarketplaceController.cs`, `ShipsController.cs`, `CatalogueController.cs`, `PlayersController.cs`
  - [ ] `BelterLife.Gateway/Auth/` — `JwtConfig.cs`, `IdentitySetup.cs` stubs
  - [ ] `BelterLife.Gateway/Routing/` — `RegionRegistry.cs`, `PlayerRouter.cs` stubs
  - [ ] `BelterLife.Admin/Api/v1/` — `ShardsController.cs`, `PlayersController.cs` stubs
  - [ ] `BelterLife.Admin/Services/` — `UniverseResetService.cs` stub
  - [ ] Configure `AppDbContext` in `BelterLife.Simulation/Infrastructure/AppDbContext.cs`:
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

- [ ] Task 4 — Scaffold Vite TypeScript client (AC: 3)
  - [ ] Run `npm create vite@latest client -- --template vanilla-ts` from repo root
  - [ ] Install client dependencies: `npm install pixi.js@8 @microsoft/signalr @microsoft/signalr-protocol-msgpack`
  - [ ] Install dev dependency: `npm install -D tailwindcss @radix-ui/themes`
  - [ ] Create `client/src/` folder structure (empty stubs):
    - `rendering/Renderer.ts`, `rendering/layers/BackgroundLayer.ts`, `WorldLayer.ts`, `EffectsLayer.ts`, `UiLayer.ts`
    - `rendering/entities/AsteroidRenderer.ts`, `ShipRenderer.ts`, `WreckRenderer.ts`
    - `state/WorldState.ts`
    - `input/InputManager.ts`, `TouchInput.ts`, `KeyboardInput.ts`
    - `network/GameHubClient.ts`, `RestClient.ts`
    - `navigation/NavigationCatalogueProjector.ts`
    - `ui/ContextualPanel.ts`, `HyperspaceMap.ts`, `MarketplaceUi.ts`, `ShipLoadoutUi.ts`
    - `types/index.ts`
  - [ ] Update `client/src/main.ts` to just import and call `app.ts`
  - [ ] Verify `npm run build` exits 0 with no TypeScript errors

- [ ] Task 5 — Create Dockerfiles (AC: 1)
  - [ ] `infra/docker/Dockerfile.gateway` — multi-stage: `dotnet publish` → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image; EXPOSE 80
  - [ ] `infra/docker/Dockerfile.shard` — same pattern for `BelterLife.Simulation`; EXPOSE 5001 (internal shard port)
  - [ ] `infra/docker/Dockerfile.admin` — same pattern for `BelterLife.Admin`; EXPOSE 5002 (internal only)

- [ ] Task 6 — Create `docker-compose.yml` for local dev (AC: 1)
  - [ ] Define services: `gateway`, `shard`, `postgres`
  - [ ] `postgres`: image `postgres:16`, env `POSTGRES_DB=belterlife`, `POSTGRES_USER`, `POSTGRES_PASSWORD` from `.env`
  - [ ] `shard`: build from `Dockerfile.shard`; depends_on `postgres`; env `ConnectionStrings__Default` pointing to postgres; env `X-Shard-Secret` from `.env`; expose internal port 5001
  - [ ] `gateway`: build from `Dockerfile.gateway`; depends_on `postgres`, `shard`; env `ConnectionStrings__Default`, `JwtKey`, `Shard__BaseUrl=http://shard:5001`; publish port `5000:80`
  - [ ] Note in README: Vite dev server run separately via `npm run dev` from `/client` (not in docker-compose — HMR requires native Node)

- [ ] Task 7 — Create minimal Kubernetes manifests (infra/k8s) (AC: 4)
  - [ ] `infra/k8s/gateway/deployment.yaml` + `service.yaml` — ClusterIP service, readinessProbe on `/health`
  - [ ] `infra/k8s/shard/deployment.yaml` + `service.yaml` — headless or ClusterIP, internal only
  - [ ] `infra/k8s/admin-api/deployment.yaml` + `service.yaml`
  - [ ] `infra/k8s/configmap.yaml` — placeholder keys: `TICK_RATE_HZ`, `REGION_WIDTH`, `REGION_HEIGHT`
  - [ ] Note: Secrets (DB string, JWT key, X-Shard-Secret) are NOT in manifests — created manually via `kubectl create secret` or injected by CI

- [ ] Task 8 — Create GitHub Actions CI pipeline (AC: 4)
  - [ ] `.github/workflows/ci.yml`:
    - Trigger: `pull_request` to `main`
    - Jobs: `build-server` (dotnet restore → build → test) + `build-client` (npm ci → npm run build)
    - .NET version: `10.x`; Node version: `20.x`
    - Use `actions/setup-dotnet@v4` and `actions/setup-node@v4`
  - [ ] `.github/workflows/deploy.yml`:
    - Trigger: `push` to `main`
    - Jobs: push images to DigitalOcean Container Registry + apply k8s manifests (placeholder — no real credentials in this story)

- [ ] Task 9 — Create `.env.example` (AC: 1)
  - [ ] Include: `POSTGRES_USER=`, `POSTGRES_PASSWORD=`, `POSTGRES_DB=belterlife`, `JWT_KEY=`, `SHARD_SECRET=`, `SHARD_BASE_URL=http://shard:5001`

- [ ] Task 10 — Verify end-to-end AC (AC: 1, 2, 3, 4)
  - [ ] Run `dotnet build server/BelterLife.sln` — expect 0 errors
  - [ ] Run `cd client && npm run build` — expect 0 TypeScript errors
  - [ ] Copy `.env.example` to `.env`, fill placeholders, run `docker-compose up` — expect all 3 services healthy
  - [ ] Open a PR to trigger CI and confirm it goes green

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

_to be filled by dev agent_

### Debug Log References

### Completion Notes List

### File List
