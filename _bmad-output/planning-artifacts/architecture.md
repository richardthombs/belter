---
stepsCompleted: ['step-01-init', 'step-02-context', 'step-03-starter', 'step-04-decisions', 'step-05-patterns', 'step-06-structure', 'step-07-validation']
status: 'complete'
completedAt: '2026-02-21'
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/product-brief-xx-2026-02-19.md'
  - '_bmad-output/planning-artifacts/ux-design-specification.md'
workflowType: 'architecture'
project_name: 'xx'
user_name: 'Richard'
date: '2026-02-21'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

42 FRs organized across 8 domains: Flight & Navigation (FR1–6), World & Belt (FR7–10, FR41–42), Mining & Resources (FR11–15), Economy & Marketplace (FR16–21), Ships & Fleet (FR22–25), Information Economy (FR26–30), Player Account & Session (FR31–34), UI (FR35), Administration (FR36–40).

The entity handoff mechanism required for player traversal (FR9), shard splitting (FR41), and shard coalescing (FR42) is the same underlying capability and must be designed as a single coherent system — this is the architectural load-bearer.

**Non-Functional Requirements:**

| NFR | Architectural Implication |
|---|---|
| 30–60 FPS server tick rate (NFR2) | Tight server loop; rules out most traditional web frameworks for the simulation layer |
| WebSocket bidirectional at 30–60 FPS (NFR3) | Significant per-shard bandwidth budgeting required |
| Zero data loss on restart (NFR6) | Persistence strategy must support hot reload or at minimum fast recovery |
| Shard failure isolation (NFR7) | State must not be shared in-memory across shards |
| Entity handoff without observable interruption (NFR17) | "Observable" threshold to be defined; drives handoff protocol design |
| Server-authoritative physics (NFR12) | No client-submitted state accepted without server validation |
| TLS/WSS in transit (NFR14) | All comms encrypted; no plain WebSocket |

Safari WebGL (NFR1) and WebSocket behaviour require early validation — historically lags on WebGL features.

**Scale & Complexity:**

- Primary domain: Full-stack, real-time multiplayer, browser-native delivery
- Complexity level: **High** — real-time distributed physics, novel game mechanics, cross-platform input
- Solo developer — scope discipline is an architectural constraint; over-engineering is an existential risk

### Technical Constraints & Dependencies

- Browser-only delivery (no native wrapper in Phase 1); Safari explicit requirement
- Server-authoritative physics — client is a dumb renderer of server state
- No client-side prediction (unless trivially implementable)
- Per-player navigation catalogue: per-asteroid keyed data; trajectory invalidation driven by physics events (collisions), not timers
- Global marketplace with dual fulfilment: physical goods require travel; information (survey data) delivered instantly
- Multi-shard world with fluid region boundaries; shards own spatial regions entirely

### Cross-Cutting Concerns Identified

| Concern | Touches |
|---|---|
| Entity handoff | Shard traversal, region splitting/coalescing, server restart recovery |
| Real-time state sync | Physics simulation, client rendering, WebSocket layer |
| Per-player navigation catalogue | Information economy, hyperspace safety query, marketplace |
| Server authority & cheat prevention | Physics, all player-submitted actions |
| Persistent state durability | All player/world data, restart recovery, universe reset |
| Dual input modality (touch + keyboard/mouse) | Every interactive UI element — must be abstracted from day one |
| Contextual proximity UI | Client distance awareness, rendering state machine |
| Admin operations | Shard monitoring, rolling restarts, player lookup, universe reset |

## Starter Template Evaluation

### Primary Technology Domain

Custom multi-process architecture: .NET 10 game server (simulation + gateway) + Vite/TypeScript browser client. Not a single-starter project — two distinct scaffolds composed under a monorepo structure.

### Selected Scaffolds

**Server:** .NET 10 Worker Service (simulation loop) + ASP.NET Core (WebSocket gateway) composed in a single solution
**Client:** Vite vanilla-ts + PixiJS v8

**Initialization Commands:**

```bash
# Server
dotnet new sln -n BelterLife
dotnet new worker -n BelterLife.Simulation
dotnet new webapi -n BelterLife.Gateway
dotnet new classlib -n BelterLife.Shared

# Client
npm create vite@latest client -- --template vanilla-ts
cd client && npm install pixi.js
```

### Architectural Decisions Established by Stack Choice

**Language & Runtime:** C# / .NET 10 (server) · TypeScript strict (client)

**Rendering:** PixiJS v8 — 2D WebGL, WebGPU-ready, Safari-compatible fallback

**Real-time Communication:** ASP.NET Core SignalR + MessagePack protocol
- MessagePack overhead ~8 bytes/message vs ~50 bytes for JSON; negligible bandwidth cost at tick rate
- Enabled via: `services.AddSignalR().AddMessagePackProtocol()`

**Database:** PostgreSQL (DigitalOcean Managed) · EF Core 10

**Containerisation:** Docker + Kubernetes on DOKS · each shard = one Kubernetes Deployment; scaling = pod count

**Project Structure:** Monorepo — `/server` (solution), `/client` (Vite app), `/infra` (k8s manifests), `docker-compose.yml` for local dev

**Note:** Project initialisation using the scaffold commands above should be the first implementation story.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- Entity handoff mechanism — single coherent system for traversal, split, and coalesce
- Region registry — PostgreSQL-backed, gateway-cached via IMemoryCache
- Server-authoritative physics — no client state accepted without server validation
- Authentication — ASP.NET Core Identity + JWT; tokens passed as query param on SignalR upgrade

**Important Decisions (Shape Architecture):**
- Inter-shard communication — direct HTTP via K8s service DNS; clean internal API contract to allow broker substitution later
- Navigation catalogue storage — relational rows (player_id, asteroid_id, trajectory, composition, recorded_at)
- Client trajectory projection — pure client-side linear extrapolation; server validates actual jump safety
- Vector graphics — PixiJS Graphics API + cacheAsTexture() for render performance

**Deferred Decisions (Post-MVP):**
- Redis cache — add when gateway scales to multiple pods; IMemoryCache sufficient for Phase 1
- Message broker — add if handoff protocol complexity demands it; HTTP sufficient for Phase 1
- WebGPU renderer — lazy-loaded when available; WebGL primary for Phase 1

### Data Architecture

| Decision | Choice | Rationale |
|---|---|---|
| ORM approach | EF Core 10, Code-First | Greenfield; C# models drive schema; migrations managed in code |
| Caching | IMemoryCache (per-process) | Sufficient for single gateway pod; Redis deferred until horizontal gateway scaling |
| Navigation catalogue | Relational rows: (player_id, asteroid_id, trajectory_data, composition_data, recorded_at) | Clean model, indexed lookups, EF-natural |
| Region registry | PostgreSQL `regions` table + IMemoryCache on gateway | Durable shared state; invalidated on every split/coalesce |

### Authentication & Security

| Decision | Choice |
|---|---|
| Auth mechanism | ASP.NET Core Identity + JWT (stateless) |
| Password hashing | ASP.NET Core Identity default (PBKDF2/HMACSHA256) |
| Role separation | ASP.NET Core Identity roles — `Admin` role gates all admin endpoints |
| SignalR auth | JWT passed as `access_token` query parameter on WebSocket upgrade (browser limitation) |

### API & Communication Patterns

| Decision | Choice |
|---|---|
| Transport split | SignalR hub: game state + player input. REST: auth, marketplace, loadout, admin, catalogue reads |
| REST versioning | URL versioning — `/api/v1/...` |
| Error format | RFC 9457 Problem Details (`application/problem+json`) via ASP.NET Core `TypedResults.Problem()` |
| Inter-shard transport | Direct HTTP via Kubernetes service DNS — clean internal API contract; broker-substitutable |
| Region registry updates | Shard writes to PostgreSQL on split/coalesce; gateway invalidates IMemoryCache |

**Inter-shard communication patterns defined:**
1. **Player/entity traversal** — request/response: source confirms destination ready before releasing entity
2. **Bulk region transfer (split/coalesce)** — push + confirmation: full entity state transferred, source waits for ack before redirecting players
3. **Region registry** — DB write on topology change; not messaging
4. **Admin drain** — command/acknowledge: `preStop` K8s lifecycle hook triggers graceful drain before SIGTERM
5. **Marketplace/economy** — DB-mediated; no direct shard-to-shard comms

### Frontend Architecture

| Decision | Choice |
|---|---|
| State management | Plain TypeScript `WorldState` module — mutated by SignalR handlers, consumed by PixiJS tick. No reactive framework. |
| Input abstraction | `InputManager` normalises touch + keyboard/mouse → unified `InputEvent { thrust: Vector2; brake: boolean; interact: boolean }` from day one |
| Rendering approach | PixiJS Graphics API (vector) + `cacheAsTexture()` for static/semi-static shapes; cache cleared on fragmentation |
| Rendering layers | Stage → BackgroundLayer → WorldLayer → EffectsLayer → UILayer |
| Navigation catalogue projection | `NavigationCatalogueProjector` — pure client-side linear extrapolation; `confidence` score (0–1) based on data age drives visual uncertainty; server validates actual jump safety |
| Asset strategy | No sprite sheets; PixiJS Assets manifest for any raster assets (UI icons etc.); WebGPU renderer lazy-loaded |

### Infrastructure & Deployment

| Decision | Choice |
|---|---|
| Service topology | `gateway` (public, stateless, horizontally scalable) · `shard` (internal, one per region) · `admin-api` (internal, single pod) · PostgreSQL (DigitalOcean Managed) |
| CI/CD | GitHub Actions — build → test → push to DigitalOcean Container Registry → apply K8s manifests |
| Logging | Serilog (structured JSON) + `kubectl logs`; log aggregation deferred until needed |
| Config management | K8s Secrets (DB strings, JWT key) + ConfigMaps (tick rate, region params); read via `IConfiguration` env vars |
| Local development | docker-compose: gateway + one shard + PostgreSQL + Vite dev server (HMR) |
| Rolling restarts | K8s `RollingUpdate` strategy + `preStop` lifecycle hook triggers graceful drain before pod receives SIGTERM |

### Decision Impact Analysis

**Implementation Sequence:**
1. Scaffold monorepo structure (server solution + client Vite app + infra)
2. Entity handoff protocol — design and validate before building game systems on top
3. Shard physics loop and region ownership
4. Gateway: auth, SignalR hub, REST API, player-to-shard routing
5. Core game systems (flight, mining, economy) built on validated shard foundation
6. Client: InputManager, WorldState, PixiJS render loop, SignalR integration
7. NavigationCatalogueProjector + hyperspace jump UI

**Cross-Component Dependencies:**
- Region registry (DB) must exist before gateway can route players to shards
- Entity handoff API must be defined before shard or gateway can implement traversal
- JWT auth must be implemented before any SignalR hub connections are secured
- InputManager abstraction must be in place before any input-dependent game features are built

## Implementation Patterns & Consistency Rules

### Naming Patterns

**Database (PostgreSQL + EF Core):**
- Table names: `snake_case` plural — `players`, `asteroids`, `navigation_catalogue_entries`
- Column names: `snake_case` — `player_id`, `recorded_at`, `composition_data`
- Foreign keys: `{table_singular}_id` — `player_id`, `asteroid_id`
- Enabled via: `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions package)

**C# Code:**
- Classes, methods, properties, constants: `PascalCase`
- Local variables, parameters, private fields: `camelCase` (no underscore prefix)

**TypeScript Code:**
- Classes: `PascalCase`
- Functions, variables: `camelCase`
- Class files: `PascalCase.ts` (`WorldState.ts`)
- Module files: `camelCase.ts` (`inputManager.ts`)

**REST API Endpoints:**
- Plural nouns, kebab-case path segments: `/api/v1/players`, `/api/v1/navigation-catalogue`
- Route parameters: `{id}` (ASP.NET Core convention)
- Query parameters: `camelCase` — `?playerId=...`

**SignalR Hub Methods:**
- Server → Client messages: `PascalCase` — `WorldStateUpdate`, `EntityHandoff`, `PlayerRedirect`
- Client → Server methods: `PascalCase` — `SendInput`, `InitiateJump`, `ScanAsteroid`

### Format Patterns

**JSON field naming:** `camelCase` throughout — ASP.NET Core default serialisation; TypeScript client expects camelCase natively.

**API responses:**
- Success: direct object in response body; no envelope wrapper
- List endpoints with pagination: `{ items: [...], total: 123 }`
- Errors: RFC 9457 Problem Details (`application/problem+json`)

**Date/time:**
- All REST API timestamps: ISO 8601 UTC strings — `"2026-02-21T07:45:00Z"`
- High-frequency SignalR game messages: Unix milliseconds (integer) for efficiency

### Structure Patterns

**Test file locations:**
- Server: `*.Tests` projects alongside production projects — `BelterLife.Simulation.Tests/`
- Client: co-located `*.test.ts` files next to the module under test

**Server project organisation (feature-vertical slices):**
```
BelterLife.Simulation/
├── Physics/         # Simulation loop, collision detection
├── Entities/        # Asteroid, Ship, Player models
├── Sharding/        # Handoff protocol, region management
└── Infrastructure/  # DB, persistence

BelterLife.Gateway/
├── Hubs/            # SignalR hubs
├── Api/             # REST controllers (versioned)
├── Auth/            # JWT, Identity configuration
└── Routing/         # Player-to-shard routing logic
```

### Enforcement Guidelines

**All AI agents MUST:**
- Use `snake_case` for all PostgreSQL identifiers (enforced by EFCore.NamingConventions)
- Use `camelCase` private fields — no underscore prefix
- Use `camelCase` for all JSON fields on the wire
- Use ISO 8601 UTC for REST timestamps; Unix ms integers for SignalR game messages
- Use RFC 9457 Problem Details for all error responses — never custom error envelopes
- Name SignalR messages in `PascalCase`
- Place new server code in the appropriate feature-vertical folder, not a generic `Helpers/` or `Utils/`

**Anti-patterns to avoid:**
- ❌ `_camelCase` private fields
- ❌ Custom error response shapes (`{ error: "...", code: 123 }`)
- ❌ `snake_case` in JSON API responses
- ❌ Mixing REST and SignalR for the same concern
- ❌ PascalCase PostgreSQL table/column names
- ❌ Flat `Controllers/` or `Services/` folders that mix unrelated concerns

## Project Structure & Boundaries

### Complete Project Directory Structure

```
belter-life/
├── .github/
│   └── workflows/
│       ├── ci.yml                    # Build, test, push on PR
│       └── deploy.yml                # Push to DOKS on main
├── infra/
│   ├── k8s/
│   │   ├── gateway/
│   │   │   ├── deployment.yaml
│   │   │   └── service.yaml
│   │   ├── shard/
│   │   │   ├── deployment.yaml
│   │   │   └── service.yaml
│   │   ├── admin-api/
│   │   │   ├── deployment.yaml
│   │   │   └── service.yaml
│   │   └── configmap.yaml            # Tick rate, region params etc.
│   └── docker/
│       ├── Dockerfile.gateway
│       ├── Dockerfile.shard
│       └── Dockerfile.admin
├── server/
│   ├── BelterLife.sln
│   ├── BelterLife.Shared/            # Domain contracts shared across projects
│   │   ├── Entities/
│   │   │   ├── Asteroid.cs
│   │   │   ├── Ship.cs
│   │   │   ├── Player.cs
│   │   │   └── Wreck.cs
│   │   ├── Contracts/
│   │   │   ├── Handoff/              # Entity handoff request/response types
│   │   │   ├── Hubs/                 # SignalR message types
│   │   │   └── Api/                  # REST DTO types
│   │   └── BelterLife.Shared.csproj
│   ├── BelterLife.Simulation/        # Physics loop + shard logic (Worker Service)
│   │   ├── Physics/
│   │   │   ├── SimulationLoop.cs     # IHostedService game tick (30-60 FPS)
│   │   │   ├── PhysicsEngine.cs      # Newtonian motion, collision detection
│   │   │   ├── CollisionResolver.cs
│   │   │   └── RegionBounds.cs
│   │   ├── Entities/
│   │   │   ├── AsteroidManager.cs    # FR7, FR8, FR11-15
│   │   │   ├── ShipManager.cs        # FR1-6, FR22-25
│   │   │   ├── PlayerSession.cs      # FR31-34
│   │   │   └── NpcManager.cs         # FR10, FR20
│   │   ├── Sharding/
│   │   │   ├── HandoffService.cs     # FR9, FR41, FR42
│   │   │   ├── RegionSplitter.cs     # FR41
│   │   │   ├── RegionCoalescer.cs    # FR42
│   │   │   └── ShardClient.cs        # HTTP client for shard-to-shard calls
│   │   ├── Economy/
│   │   │   ├── MarketplaceService.cs # FR16-21
│   │   │   ├── NpcPricing.cs         # FR20
│   │   │   └── ContractService.cs    # FR14
│   │   ├── Information/
│   │   │   ├── CatalogueService.cs   # FR26-30
│   │   │   └── BeaconService.cs      # FR15
│   │   ├── Infrastructure/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   ├── Program.cs
│   │   └── BelterLife.Simulation.csproj
│   ├── BelterLife.Simulation.Tests/
│   │   ├── Physics/
│   │   ├── Sharding/
│   │   └── Economy/
│   ├── BelterLife.Gateway/           # Public-facing ASP.NET Core host
│   │   ├── Hubs/
│   │   │   └── GameHub.cs            # SignalR hub — player input + world state
│   │   ├── Api/
│   │   │   └── v1/
│   │   │       ├── AuthController.cs
│   │   │       ├── MarketplaceController.cs
│   │   │       ├── ShipsController.cs
│   │   │       ├── CatalogueController.cs
│   │   │       └── PlayersController.cs
│   │   ├── Auth/
│   │   │   ├── JwtConfig.cs
│   │   │   └── IdentitySetup.cs
│   │   ├── Routing/
│   │   │   ├── RegionRegistry.cs     # "Which shard owns sector X?"
│   │   │   └── PlayerRouter.cs
│   │   ├── Program.cs
│   │   └── BelterLife.Gateway.csproj
│   ├── BelterLife.Gateway.Tests/
│   │   ├── Api/
│   │   └── Routing/
│   └── BelterLife.Admin/             # Admin API (FR36-40)
│       ├── Api/
│       │   └── v1/
│       │       ├── ShardsController.cs    # FR36, FR37
│       │       └── PlayersController.cs   # FR38
│       ├── Services/
│       │   └── UniverseResetService.cs    # FR40
│       ├── Program.cs
│       └── BelterLife.Admin.csproj
├── client/
│   ├── index.html
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── package.json
│   └── src/
│       ├── main.ts
│       ├── app.ts
│       ├── rendering/
│       │   ├── Renderer.ts
│       │   ├── layers/
│       │   │   ├── BackgroundLayer.ts
│       │   │   ├── WorldLayer.ts
│       │   │   ├── EffectsLayer.ts
│       │   │   └── UiLayer.ts            # FR35
│       │   └── entities/
│       │       ├── AsteroidRenderer.ts   # Vector polygon + cacheAsTexture
│       │       ├── ShipRenderer.ts
│       │       └── WreckRenderer.ts
│       ├── state/
│       │   └── WorldState.ts
│       ├── input/
│       │   ├── InputManager.ts           # Normalises touch + keyboard → InputEvent
│       │   ├── TouchInput.ts
│       │   └── KeyboardInput.ts
│       ├── network/
│       │   ├── GameHubClient.ts          # SignalR + MessagePack
│       │   └── RestClient.ts
│       ├── navigation/
│       │   └── NavigationCatalogueProjector.ts  # Client-side trajectory projection
│       ├── ui/
│       │   ├── ContextualPanel.ts
│       │   ├── HyperspaceMap.ts
│       │   ├── MarketplaceUi.ts
│       │   └── ShipLoadoutUi.ts
│       └── types/
│           └── index.ts
├── docker-compose.yml
├── .env.example
└── README.md
```

### Architectural Boundaries

**Public boundary (internet-facing):**
- `gateway` only — all player traffic enters here; TLS/WSS termination at K8s ingress
- `admin-api` — internal only, never publicly exposed

**Internal boundary (K8s cluster-internal):**
- `gateway` ↔ `shard` — HTTP handoff API + player redirect
- `shard` ↔ `shard` — HTTP entity transfer on boundary crossing
- All services → PostgreSQL via DigitalOcean private network

**Data boundary:**
- Each shard owns its region's in-memory simulation state exclusively — no shared in-process state across pods
- PostgreSQL is the single source of truth for all persistent state
- `regions` table is the authoritative region registry; gateway caches it in IMemoryCache, invalidated on every split/coalesce

### Requirements to Structure Mapping

| FR Group | Server location | Client location |
|---|---|---|
| Flight & Navigation (FR1–6) | `Simulation/Entities/ShipManager.cs` | `input/`, `rendering/entities/ShipRenderer.ts` |
| World & Belt (FR7–10, FR41–42) | `Simulation/Physics/`, `Simulation/Sharding/` | `rendering/layers/WorldLayer.ts` |
| Mining & Resources (FR11–15) | `Simulation/Entities/AsteroidManager.cs` | `ui/ContextualPanel.ts` |
| Economy & Marketplace (FR16–21) | `Simulation/Economy/`, `Gateway/Api/v1/MarketplaceController.cs` | `ui/MarketplaceUi.ts` |
| Ships & Fleet (FR22–25) | `Simulation/Entities/ShipManager.cs`, `Gateway/Api/v1/ShipsController.cs` | `ui/ShipLoadoutUi.ts` |
| Information Economy (FR26–30) | `Simulation/Information/CatalogueService.cs` | `navigation/NavigationCatalogueProjector.ts`, `ui/HyperspaceMap.ts` |
| Player Account & Session (FR31–34) | `Gateway/Auth/`, `Gateway/Api/v1/AuthController.cs` | `network/RestClient.ts` |
| UI (FR35) | — | `ui/ContextualPanel.ts`, `rendering/layers/UiLayer.ts` |
| Administration (FR36–40) | `Admin/Api/v1/` | Separate admin panel |

## Architecture Validation Results

### Coherence Validation ✅

All technology choices version-aligned and compatible (.NET 10, EF Core 10, PixiJS v8, PostgreSQL). Feature-vertical structure consistent throughout. SignalR/REST transport split applied consistently — no FR served by both. `camelCase` JSON serialisation aligns with TypeScript client expectations.

**Known friction point (not a blocker):** `BelterLife.Shared` SignalR/API contract types are manually mirrored to `client/src/types/index.ts`. Divergence risk mitigated by keeping Shared contracts minimal. Code generation (NSwag) deferred to post-MVP.

### Requirements Coverage ✅

All 42 FRs mapped to specific implementation files. All NFRs architecturally addressed.

| NFR | Coverage |
|---|---|
| 30–60 FPS server tick (NFR2) | `SimulationLoop.cs` as `IHostedService` — tight loop, no request overhead ✅ |
| WebSocket at 30–60 FPS (NFR3) | SignalR + MessagePack — ~8 bytes/message framing overhead ✅ |
| Zero data loss on restart (NFR6) | EF Core persistence + `preStop` drain hook ✅ |
| Shard failure isolation (NFR7) | No shared in-process state; each shard is an independent pod ✅ |
| Entity handoff without interruption (NFR17) | `HandoffService.cs` — confirmation before entity release ✅ |
| Server-authoritative physics (NFR12) | All game state owned by Simulation; client is read-only renderer ✅ |
| TLS/WSS (NFR14) | K8s ingress terminates TLS; internal traffic on private network ✅ |
| Horizontal shard scaling (NFR15) | Each shard = one K8s Deployment; new pods = new regions ✅ |
| Rolling restarts (NFR10) | K8s `RollingUpdate` + `preStop` drain hook ✅ |
| Safari WebGL (NFR1) | PixiJS v8 WebGL primary; Safari validation flagged as early implementation priority ✅ |

**NFR17 note:** Observability threshold for "without observable interruption" deferred to entity handoff story implementation. Known acceptable gap.

### Security Gap Resolved

Shard-to-shard internal HTTP calls authenticated via `X-Shard-Secret` shared secret header, injected as K8s Secret environment variable. All `ShardClient` outgoing calls MUST attach the header; all shard HTTP pipelines MUST validate it before processing any handoff request.

### Architecture Completeness Checklist

- [x] Requirements analysis complete — 42 FRs, all NFRs
- [x] Scale and complexity assessed — High; solo developer scope discipline enforced
- [x] Technology stack fully specified — .NET 10, EF Core 10, PixiJS v8, PostgreSQL
- [x] Critical architectural decisions documented
- [x] Inter-shard communication patterns fully defined (5 patterns)
- [x] Authentication and security decisions complete
- [x] Implementation patterns and naming conventions established
- [x] Complete project directory structure defined
- [x] All FRs mapped to specific structural locations
- [x] All NFRs architecturally addressed
- [x] Internal service authentication resolved

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION
**Confidence Level:** High

**Key Strengths:**
- Entity handoff designed as a single coherent system from the start — the architectural load-bearer is well-defined
- Server-authoritative physics with no client prediction — minimal cheat surface, clean separation
- Solo-developer scope discipline enforced architecturally — deferred decisions documented explicitly
- `NavigationCatalogueProjector` is a clean client-side concern — server stays simple, game mechanic preserved

**Deferred for post-MVP:**
- Redis cache — when gateway scales horizontally
- Message broker — if handoff protocol complexity demands it
- Type code generation (NSwag) — Shared contracts → TypeScript client
- mTLS / service mesh — security depth exploration

### Implementation Handoff

**First implementation priority:**
1. Scaffold monorepo (server solution + client Vite app + infra)
2. Entity handoff protocol — implement and validate before any game systems are built on top
3. Shard physics loop and region ownership
4. Gateway: auth, SignalR hub, player routing
5. Core game systems on validated shard foundation
6. Client: InputManager, WorldState, PixiJS render loop, SignalR integration
7. NavigationCatalogueProjector + hyperspace jump UI
