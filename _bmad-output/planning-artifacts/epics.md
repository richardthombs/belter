---
stepsCompleted: ['step-01-validate-prerequisites', 'step-02-design-epics', 'epic-01', 'epic-02', 'epic-03', 'epic-04', 'epic-05', 'epic-06', 'epic-07']
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/architecture.md'
  - '_bmad-output/planning-artifacts/ux-design-specification.md'
---

# xx (Belter Life) - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Belter Life, decomposing the requirements from the PRD, UX Design, and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Players can fly their ship using an assisted Newtonian physics model in 2D space
FR2: Players can control their ship via touch (virtual joystick) on tablet and via keyboard/mouse on desktop
FR3: Players can initiate hyperspace jumps to other sectors
FR4: Players can calculate a safe hyperspace arrival window based on their personal navigation catalogue
FR5: Players receive an indication of jump risk based on the coverage and currency of their catalogue for the target sector
FR6: Players manage fuel as a finite resource that limits deep-space exploration range
FR7: The game world consists of a procedurally generated, ever-expanding asteroid belt divided into discrete sectors
FR8: The asteroid belt is a living simulation — asteroids move, collide, fragment, and change trajectory over time
FR9: Players can traverse between sectors; the game world spans multiple server shards transparently
FR10: Players can discover NPC stations and outposts distributed across the belt, including in frontier space
FR11: Players can scan asteroids to reveal their resource composition
FR12: Players can mine asteroids to extract resources into their cargo hold
FR13: Players can claim asteroids and pay ongoing upkeep to maintain ownership
FR14: Players can post and fulfil mining rights contracts on claimed asteroids
FR15: Players can place beacons on claimed asteroids
FR16: Players can browse the global marketplace from any location in the belt
FR17: Players can list resources and survey data for sale on the global marketplace
FR18: Players must physically travel to a marketplace location to collect purchased physical goods
FR19: Purchased survey data is delivered to the buyer's navigation catalogue instantly upon transaction
FR20: NPC stations buy and sell resources at dynamically adjusted prices that serve as an economic floor
FR21: Players can purchase ship component upgrades at NPC stations
FR22: Players can own and manage a personal fleet of ships stored at stations
FR23: Players can customise ship loadout with components representing meaningful trade-offs
FR24: Players always have access to a free replacement starter ship upon losing their current ship
FR25: Destroyed ships leave persistent wrecks at their destruction location that other players can salvage
FR26: Players can acquire survey data containing asteroid trajectory and composition information
FR27: Survey data is stored per-asteroid in the player's navigation catalogue, keyed by unique asteroid ID
FR28: Acquiring new survey data for an asteroid overwrites the previous catalogue entry for that asteroid
FR29: Players can view their navigation catalogue to assess sector coverage before a hyperspace jump
FR30: Players can sell survey data on the global marketplace
FR31: New players are onboarded through contextual in-world prompts with no mandatory tutorial sequence
FR32: Players can create an account and maintain persistent game state across sessions
FR33: Players can log out and return to their exact game state (ship, location, assets, catalogue) in a later session
FR34: Player assets and progress persist indefinitely with no penalty for time spent offline
FR35: The UI presents minimal controls during open-space flight and surfaces richer interaction panels automatically when the player is in proximity to interactive objects
FR36: The administrator can view the status and player count of all active server shards
FR37: The administrator can perform rolling restarts of server shards without forcing simultaneous disconnection of all players
FR38: The administrator can look up player accounts and inspect their game state
FR39: All player and world state survives a server restart without data loss
FR40: The administrator can initiate a deliberate universe reset as an explicit operation entirely separate from normal server restart
FR41: The system detects when a shard's simulation load exceeds a threshold and automatically splits the responsible region into sub-regions
FR42: The system detects when adjacent shards are below load threshold and automatically coalesces their regions onto a single shard

### NonFunctional Requirements

NFR1: Client renders at stable 30–60 FPS on mid-range tablet and PC in a modern browser (Chrome, Firefox, Safari, Edge)
NFR2: Server physics simulation runs at stable 30–60 FPS tick rate under expected concurrent player load per shard
NFR3: WebSocket state updates flow at 30–60 FPS in both directions
NFR4: Hyperspace jump transitions complete without observable client frame drop or freeze
NFR5: Marketplace browse and transaction operations complete within 2 seconds under normal load
NFR6: All player and world state survives a server restart without data loss
NFR7: Individual shard failure does not cause data loss for players on other shards
NFR8: The server returns to full operational state after a restart without manual data repair
NFR9: Under load, degradation response is automatic region splitting (FR41); hard failure or data corruption are not acceptable
NFR10: Rolling shard restarts complete without forcing simultaneous disconnection of all players
NFR11: Player passwords are stored hashed; sessions are invalidated on logout
NFR12: The physics simulation is server-authoritative; server rejects any unvalidated client-submitted state
NFR13: Admin operations are accessible only to authenticated administrator accounts
NFR14: All client-server communication is encrypted in transit (TLS/WSS)
NFR15: The shard architecture supports horizontal scaling — new shards can be added without architectural changes
NFR16: A single shard supports a target minimum concurrent player count at the required tick rate
NFR17: Player and entity handoff between shards completes without observable interruption

### Additional Requirements

**Architecture — Infrastructure & Stack:**
- Monorepo scaffold: .NET 10 server solution (`BelterLife.Simulation`, `BelterLife.Gateway`, `BelterLife.Shared`, `BelterLife.Admin`) + Vite vanilla-ts client + `/infra` K8s manifests + `docker-compose.yml` for local dev
- Project initialisation using scaffold commands is the first implementation story
- Server: .NET 10 Worker Service (simulation loop) + ASP.NET Core (gateway) + PostgreSQL via EF Core 10 Code-First
- Client: Vite vanilla-ts + PixiJS v8 + SignalR with MessagePack protocol
- Containerisation: Docker + Kubernetes (DigitalOcean DOKS); each shard = one K8s Deployment
- CI/CD: GitHub Actions — build → test → push to DigitalOcean Container Registry → apply K8s manifests
- Local development: docker-compose (gateway + one shard + PostgreSQL + Vite HMR)
- Config: K8s Secrets (DB strings, JWT key) + ConfigMaps (tick rate, region params) via `IConfiguration` env vars

**Architecture — Critical Implementation Order:**
- Entity handoff protocol must be designed and validated before any game systems are built on top
- Region registry (DB `regions` table) must exist before gateway can route players to shards
- JWT auth must be implemented before any SignalR hub connections are secured
- InputManager abstraction must be in place before any input-dependent game features

**Architecture — Security:**
- Shard-to-shard HTTP calls authenticated via `X-Shard-Secret` shared secret header (K8s Secret env var); all ShardClient outgoing calls MUST attach it; all shard HTTP pipelines MUST validate it
- JWT passed as `access_token` query parameter on WebSocket upgrade (browser limitation)
- ASP.NET Core Identity roles — `Admin` role gates all admin endpoints

**Architecture — Consistency Rules:**
- DB identifiers: `snake_case` (EFCore.NamingConventions package — `UseSnakeCaseNamingConvention()`)
- C#: `PascalCase` for classes/methods/properties; `camelCase` for local variables/fields (no underscore prefix)
- TypeScript: `PascalCase` classes; `camelCase` functions/variables
- JSON: `camelCase` throughout; errors: RFC 9457 Problem Details; REST timestamps: ISO 8601 UTC; SignalR game messages: Unix ms integers
- SignalR server→client messages: `PascalCase`; client→server methods: `PascalCase`
- Server code: feature-vertical slices (Physics/, Entities/, Sharding/ etc.) — no flat Helpers/ or Utils/

**Architecture — Key Patterns:**
- `NavigationCatalogueProjector`: pure client-side linear extrapolation; server validates actual jump safety
- Safe-spawn position check on re-entry uses same spatial awareness system as hyperspace collision detection (one coherent system)
- IMemoryCache (per-process) for region registry on gateway; Redis deferred to post-MVP
- Inter-shard comms: direct HTTP via K8s service DNS; 5 defined patterns (traversal, bulk transfer, registry, admin drain, marketplace)
- PixiJS rendering: Stage → BackgroundLayer → WorldLayer → EffectsLayer → UILayer; vector graphics + `cacheAsTexture()` for static shapes

**UX — Browser & Device:**
- Browser support: Chrome, Firefox, Safari, Edge (all evergreen); screen size floor ~768px wide (iPad portrait minimum)
- Design lead device: iPad (touch-first); PC (keyboard/mouse as enhancement, not separate UX path)
- All interactions completable by touch alone; keyboard shortcuts are enhancements
- Touch targets ≥44×44px minimum; 48×48px preferred for primary actions

**UX — Accessibility:**
- WCAG AA contrast (4.5:1 body text; 3:1 large text) across all text tokens
- No colour-only information encoding — density and shape used alongside colour in catalogue visualisation
- Keyboard navigation via Radix UI focus management; focus trapped in open overlays; Escape to dismiss
- `prefers-reduced-motion` respected — no animation, just appear/disappear

**UX — Rendering & Component Architecture:**
- Two UI layers: Layer 1 — PixiJS canvas (game rendering, no HTML); Layer 2 — HTML overlay (panels, marketplace, catalogue) via Tailwind + Radix UI
- CSS custom property token bridge: both layers read the same token set; PixiJS reads values via `getComputedStyle` at runtime
- Fonts: Inter (primary, all panels/labels); DM Mono (monospaced, station names/sector IDs/catalogue timestamps)
- Base spacing unit: 8px; all spacing in multiples of 8

**UX — Interaction Patterns:**
- Context panel updates live as player approaches — no re-tap required; options appear/grey-out as range thresholds crossed
- Pulse animation spec: 150ms ease-out scale to 1.08 + brightness +20%, return over 300ms; configurable intensity (`subtle` | `emphatic`)
- Materialisation beat: shared animation for hyperspace arrivals and session re-entry — 2–3 seconds, sector resolves from centre outward, quiet scan window, then bottom bar fades in
- On re-entry: if spawn position occupied, nudge to nearest safe point + gentle notification; ship is never destroyed for offline hazards

**UX — Phase 1 MVP Components (must ship):**
- Context Panel (defines the entire interaction model)
- HUD Bottom Bar + Pulse Indicator (ambient situational awareness)
- Materialisation Screen (hyperspace + re-entry beat)
- Star Map (hyperspace navigation)

**UX — Phase 2 Components:**
- Data Density Indicator
- Catalogue Listing Card (information marketplace)
- Tutorial Tooltip (new player onboarding — first session only, max 4 prompts, auto-advances, never shown again)

### FR Coverage Map

| FR | Epic | Summary |
|---|---|---|
| FR1 | Epic 1 | Newtonian ship flight |
| FR2 | Epic 1 | Touch + keyboard/mouse controls |
| FR3 | Epic 3 | Initiate hyperspace jumps |
| FR4 | Epic 3 | Safe arrival window from catalogue |
| FR5 | Epic 3 | Jump risk indication from coverage |
| FR6 | Epic 2 | Fuel management |
| FR7 | Epic 1 | Procedural asteroid belt (single sector) |
| FR8 | Epic 2 | Living belt — asteroids move, collide, fragment |
| FR9 | Epic 4 | Cross-sector/multi-shard traversal |
| FR10 | Epic 2 (local stations) / Epic 5 (frontier outposts) | NPC stations and outposts |
| FR11 | Epic 2 | Asteroid scanning |
| FR12 | Epic 2 | Asteroid mining |
| FR13 | Epic 5 | Asteroid claiming + upkeep |
| FR14 | Epic 5 | Mining rights contracts |
| FR15 | Epic 5 | Beacon placement |
| FR16 | Epic 3 (information) / Epic 5 (full) | Global marketplace browse |
| FR17 | Epic 3 (survey data) / Epic 5 (resources) | Listing items for sale |
| FR18 | Epic 5 | Physical goods require travel to collect |
| FR19 | Epic 3 | Survey data delivered instantly on purchase |
| FR20 | Epic 2 | NPC dynamic pricing floor |
| FR21 | Epic 5 | Ship component upgrades at stations |
| FR22 | Epic 5 | Personal fleet management |
| FR23 | Epic 5 | Ship loadout customisation |
| FR24 | Epic 2 | Free starter ship on loss |
| FR25 | Epic 5 | Persistent wrecks + salvage |
| FR26 | Epic 3 | Acquire survey data (scan → catalogue) |
| FR27 | Epic 3 | Per-asteroid navigation catalogue |
| FR28 | Epic 3 | New survey data overwrites previous entry |
| FR29 | Epic 3 | View catalogue before jump |
| FR30 | Epic 3 | Sell survey data on marketplace |
| FR31 | Epic 7 | Contextual in-world onboarding prompts |
| FR32 | Epic 1 | Account creation + persistent state |
| FR33 | Epic 1 | Session persistence (log out/return) |
| FR34 | Epic 1 | Offline-penalty-free persistence |
| FR35 | Epic 1 (minimal HUD) / Epic 2 (full proximity UI) | Contextual proximity UI |
| FR36 | Epic 6 | Admin: shard status + player count |
| FR37 | Epic 6 | Admin: rolling restarts |
| FR38 | Epic 6 | Admin: player account lookup |
| FR39 | Epic 4 | State survives server restart |
| FR40 | Epic 6 | Admin: deliberate universe reset |
| FR41 | Epic 4 | Auto shard splitting under load |
| FR42 | Epic 4 | Auto shard coalescing under low load |

## Epic List

### Epic 1: Foundation — A Player Can Join and Fly
A player can create an account, connect to a live game world, fly their ship in a sector using touch or keyboard controls, and return to their exact state on a future login. The monorepo is scaffolded, the render loop is running, and SignalR state sync is live.
**FRs covered:** FR1, FR2, FR7 (single-sector procedural gen), FR32, FR33, FR34, FR35 (minimal HUD)
**Architecture note:** Story 1.1 = monorepo scaffold (dotnet new + vite). Enables all future epics.

### Epic 2: The Core Mining Loop — Discover, Extract, Sell
A player can scan asteroids to discover composition, mine resources into their cargo hold, manage fuel, dock at NPC stations, and sell cargo for credits at a dynamic NPC-floor price. If their ship is destroyed by a collision, they receive a free starter replacement.
**FRs covered:** FR6, FR8, FR10 (local sector stations), FR11, FR12, FR20, FR24, FR35 (full proximity panel)
**UX note:** Context Panel, HUD Bottom Bar, and Pulse Indicator ship in this epic.

### Epic 3: The Navigation Catalogue & Hyperspace Jumps
A player can build a personal per-asteroid navigation catalogue through scanning, view data density across sectors on a Star Map, buy and sell survey data on the information marketplace, and execute hyperspace jumps to other sectors with the full materialisation beat. Jump risk is readable at a glance — never a safety verdict.
**FRs covered:** FR3, FR4, FR5, FR19, FR26, FR27, FR28, FR29, FR30, FR16 (information marketplace)
**UX note:** Star Map, Materialisation Screen, Data Density Indicator, Catalogue Listing Card ship here.
**Architecture note:** Hyperspace built within a single shard (multiple sectors, one shard) — client mechanic established here. Multi-shard distribution follows in Epic 4.

### Epic 4: A Distributed World — Multi-Shard & Auto-Scaling
The game world transparently spans multiple server shards. Players traverse sector boundaries without interruption. The world auto-splits under load and coalesces when quiet. All state survives a server restart. Rolling restarts proceed without mass disconnection.
**FRs covered:** FR9, FR39, FR41, FR42
**NFRs addressed:** NFR6, NFR7, NFR8, NFR9, NFR10, NFR15, NFR16, NFR17
**Architecture note:** Entity handoff protocol is the load-bearer of this epic — design validated before shipping game code on top.

### Epic 5: Full Economy — Marketplace, Ships, and Territory
Players can trade resources and survey data on a global player marketplace (with physical goods requiring travel to collect), manage a personal fleet, customise ship loadouts, claim asteroids with upkeep, post and fulfil mining rights contracts, place beacons on claimed asteroids, purchase ship upgrades at stations, and salvage wrecks left by destroyed ships.
**FRs covered:** FR13, FR14, FR15, FR16 (full), FR17, FR18, FR21, FR22, FR23, FR25, FR10 (frontier outposts)
**Note:** FR16/FR17 partial coverage began in Epic 3 (information side); this epic completes physical goods and resource trading.

### Epic 6: Administration & Live Service Operations
Administrators can monitor all active shards and player counts, perform rolling restarts without mass disconnection, look up and inspect player accounts, execute a deliberate universe reset as an explicit operation, and trust that all state is preserved across restarts.
**FRs covered:** FR36, FR37, FR38, FR40
**NFRs addressed:** NFR13

### Epic 7: New Player Experience & Contextual Discovery
A new player discovers how to fly, scan, mine, and sell through contextual in-world prompts — no mandatory tutorial screen. The first completed mining cycle (fly → mine → sell → credits) happens within 5 minutes of first login. After 4 contextual prompts, the world goes quiet and the contextual UI model takes over.
**FRs covered:** FR31
**UX note:** Tutorial Tooltip component (Phase 2 UX) ships here.

---

## Epic 1: Foundation — A Player Can Join and Fly

A player can create an account, connect to a live game world, fly their ship in a sector using touch or keyboard controls, and return to their exact state on a future login. The monorepo is scaffolded, the render loop is running, and SignalR state sync is live.

### Story 1.1: Monorepo Scaffold & Local Dev Environment

As a **developer**,
I want a configured monorepo with .NET 10 server solution, Vite TypeScript client, infra manifests, and a working docker-compose environment,
So that all future development has a consistent, runnable foundation from day one.

**Acceptance Criteria:**

**Given** the repository is cloned,
**When** `docker-compose up` is run,
**Then** a gateway service, one shard service, and a PostgreSQL instance all start without errors
**And** the Vite dev server can be started at localhost with HMR enabled

**Given** the server solution,
**When** `dotnet build` is run from `/server`,
**Then** `BelterLife.Simulation`, `BelterLife.Gateway`, `BelterLife.Shared`, and `BelterLife.Admin` all compile without errors

**Given** the client project,
**When** `npm run build` is run from `/client`,
**Then** TypeScript compiles without errors

**Given** a PR is opened,
**When** the GitHub Actions CI pipeline runs,
**Then** build and test steps complete successfully

---

### Story 1.2: Player Account Registration & Login

As a **new player**,
I want to register with a username and password and receive a secure JWT token on login,
So that I have a persistent identity and can access the game.

**Acceptance Criteria:**

**Given** `POST /api/v1/auth/register` with a valid unique username and password,
**When** the request is submitted,
**Then** an account is created (password hashed via ASP.NET Core Identity PBKDF2/HMACSHA256) and a JWT token is returned

**Given** `POST /api/v1/auth/login` with valid credentials,
**When** submitted,
**Then** a JWT token is returned

**Given** invalid credentials at login,
**When** submitted,
**Then** HTTP 401 is returned with RFC 9457 Problem Details body

**Given** a duplicate username at registration,
**When** submitted,
**Then** HTTP 400 is returned with Problem Details

**Given** a valid JWT token on a protected endpoint,
**When** the request is made,
**Then** access is granted

**Given** `POST /api/v1/auth/logout`,
**When** called with a valid token,
**Then** the session is invalidated and the token is rejected on subsequent requests (NFR11)

---

### Story 1.3: Procedurally Generated Starting Sector

As a **new player**,
I want to enter a starting sector populated with procedurally generated asteroids when I first log in,
So that there is a live game world to explore from my very first session.

**Acceptance Criteria:**

**Given** a new player account,
**When** the player first logs in,
**Then** a sector is assigned and a ship entity is created at a safe spawn point within that sector

**Given** a generated sector,
**When** created,
**Then** it contains 20–50 asteroids with procedurally varied positions, shapes, and sizes (no two sectors identical)

**Given** the starting sector,
**When** populated,
**Then** at least one NPC station exists within reasonable travel distance of the spawn point

**Given** the EF Core schema,
**When** migrations run,
**Then** `sectors`, `asteroids`, `ships`, and `players` tables exist using `snake_case` naming convention (EFCore.NamingConventions)

---

### Story 1.4: Real-Time Game World via SignalR

As a **player**,
I want to see my ship and the asteroid sector rendered in real time in my browser,
So that I am connected to and experiencing the live game world.

**Acceptance Criteria:**

**Given** a logged-in player,
**When** the client connects to the SignalR `GameHub` (WSS with JWT as `access_token` query param),
**Then** the connection is established and the player is routed to their sector's shard

**Given** the established connection,
**When** the server sends `WorldStateUpdate` messages at the configured tick rate,
**Then** the PixiJS canvas renders ship position and asteroid positions in real time

**Given** the PixiJS render loop,
**When** running on a mid-range tablet,
**Then** the client maintains 30–60 FPS (NFR1)

**Given** the `SimulationLoop.cs` `IHostedService`,
**When** running,
**Then** the server tick rate is stable at 30–60 FPS (NFR2)

**Given** all WebSocket traffic,
**Then** it is encrypted via TLS/WSS (NFR14)

**Given** the rendering stage,
**When** rendering,
**Then** layers render in order: BackgroundLayer → WorldLayer → EffectsLayer → UILayer

---

### Story 1.5: Ship Flight — Assisted Newtonian Physics

As a **player**,
I want to fly my ship using assisted Newtonian physics (thrust, momentum, and soft braking),
So that movement feels physical and responsive without requiring constant correction.

**Acceptance Criteria:**

**Given** the player's ship,
**When** thrust input is applied,
**Then** the ship accelerates in the thrust direction (velocity accumulates via Newtonian physics)

**Given** a moving ship with no thrust input,
**When** assisted flight is active,
**Then** the ship decelerates gently to rest (soft assistance — not pure drift)

**Given** the ship at speed,
**When** thrust is applied in a different direction,
**Then** the velocity vector changes correctly via vector addition

**Given** any client-submitted position state,
**When** received by the server,
**Then** it is rejected — the server only accepts `InputEvent` vectors; all physics are server-authoritative (NFR12)

**Given** `SendInput` called on the SignalR hub with a valid `InputEvent`,
**When** processed by the shard,
**Then** the ship's physics state is updated and reflected in the next `WorldStateUpdate`

---

### Story 1.6: Dual Input — Touch Virtual Joystick & Keyboard/Mouse

As a **player**,
I want to control my ship using a virtual joystick on tablet or WASD/arrow keys on desktop,
So that I can play comfortably on my preferred device with no difference in capability.

**Acceptance Criteria:**

**Given** a touch device,
**When** the player holds their thumb on the virtual joystick zone (bottom-left, thumb-reach),
**Then** a visual joystick appears and ship thrust is applied proportional to displacement direction and magnitude

**Given** a keyboard,
**When** WASD or arrow keys are held,
**Then** the corresponding thrust `InputEvent` is sent to the server

**Given** either input method,
**When** processed through `InputManager`,
**Then** both produce the same `InputEvent { thrust: Vector2; brake: boolean; interact: boolean }` — no game code branches on input type

**Given** the virtual joystick touch target,
**Then** it is at least 48×48px

**Given** `prefers-reduced-motion`,
**When** enabled,
**Then** the joystick renders without animation (appear/disappear only)

---

### Story 1.7: Persistent Session — Return to Exact State

As a **player**,
I want my ship position, sector, credits, and cargo saved automatically so that I return exactly where I left off with no penalty for time offline,
So that the game respects my time and builds a habit of return.

**Acceptance Criteria:**

**Given** a player with a ship at position (x, y) in sector S,
**When** the player closes the browser or explicitly logs out,
**Then** ship position, sector, credits, and cargo are persisted to the database

**Given** persisted state,
**When** the player logs back in,
**Then** their ship appears at the saved position in the saved sector

**Given** the saved spawn position is now occupied by an asteroid,
**When** the server checks on re-entry,
**Then** the ship is placed at the nearest safe point and a notification is shown: *"Your ship was repositioned — the belt moved while you were away"*

**Given** any length of time between sessions,
**Then** no assets, credits, or progress are lost or penalised (FR34)

**Given** a server restart,
**When** the server comes back up,
**Then** all player state is fully recovered from the database without manual intervention (NFR6, NFR8)

---

## Epic 2: The Core Mining Loop — Discover, Extract, Sell

A player can scan asteroids to discover composition, mine resources into their cargo hold, manage fuel, dock at NPC stations, and sell cargo for credits at a dynamic NPC-floor price. If their ship is destroyed by a collision, they receive a free starter replacement.

### Story 2.0: int64 Coordinate System and Scale Migration

As a **developer**,  
I want all world coordinates stored as `long` (int64) with 1 unit = 1 millimetre, sector geometry standardized at 50km × 50km, and physics constants rescaled to mm units,  
So that Epic 2+ gameplay systems build on a coherent, precision-safe world model.

**Acceptance Criteria:**

**Given** core world entities (`Asteroid`, `Ship`, `NpcStation`),  
**When** persisted and transmitted,  
**Then** `X` and `Y` are int64 (`long`) millimetre coordinates

**Given** a `Sector`,  
**When** stored,  
**Then** `GridX`, `GridY` are `long` and `IsGenerated` tracks lazy generation state

**Given** canonical bounds usage,  
**When** consulted by simulation and generation logic,  
**Then** `RegionBounds.SectorSize = 50_000_000L` and `RegionBounds.HalfSector = 25_000_000L`

**Given** `PhysicsEngine` translational constants,  
**When** examined,  
**Then** they are mm-scale (`MaxSpeed = 300_000f`, `ThrustForce = 150_000f`, `RetroForce = 100_000f`) and position integration uses `(long)` casting

**Given** sector generation and spawn flows,  
**When** evaluated,  
**Then** asteroid/station placement and safe-margin checks use mm-scale ranges and `SpawnResponse`/snapshot contracts carry int64 positions

**Given** full project verification,  
**When** build and tests run,  
**Then** server/client builds pass and all existing tests pass under the new coordinate model

---

### Story 2.1: Living Belt — Asteroid Physics Simulation

As a **player**,
I want to see asteroids moving, drifting, and colliding in the belt around me,
So that the world feels alive and dynamic rather than static.

**Acceptance Criteria:**

**Given** the `SimulationLoop.cs` tick,
**When** running,
**Then** each asteroid's position is updated each tick according to its velocity vector (Newtonian drift)

**Given** two asteroids on a collision course,
**When** they meet,
**Then** collision is detected, both asteroids change trajectory based on mass/momentum, and fragments may be generated (FR8)

**Given** a collision that fragments an asteroid,
**When** it occurs,
**Then** child fragment entities are created in the database with new trajectories and the parent asteroid is marked destroyed

**Given** `AsteroidManager.cs`,
**When** updating asteroid state,
**Then** all trajectory updates are server-authoritative and reflected in the next `WorldStateUpdate` to all connected clients

---

### Story 2.2: HUD Bottom Bar & Pulse Indicator

As a **player**,
I want a persistent bottom bar showing my credits, cargo hold percentage, speed, and coarse location context (sector + approximate in-sector position) at all times during flight,
So that I always have ambient situational awareness without any interaction required.

**Acceptance Criteria:**

**Given** the player is in flight,
**When** the game canvas is visible,
**Then** the HUD bottom bar is persistently visible at the bottom edge of the viewport as a translucent overlay (never obscuring the full canvas)

**Given** the credits display,
**When** the credit value changes,
**Then** a pulse animation plays: 150ms ease-out scale to 1.08 + brightness +20%, return over 300ms

**Given** the hold capacity bar,
**When** ore is deposited,
**Then** the bar fills left-to-right and pulses (subtle intensity) on each deposit; pulses emphatic intensity and shifts colour when hold is full

**Given** the speed indicator,
**When** updating,
**Then** it shows the live numeric value with no pulse (continuous change would be noisy)

**Given** the credits and hold `aria-live` regions,
**When** values change,
**Then** screen readers announce the update (`role="status"`, `aria-live="polite"`)

**Given** `prefers-reduced-motion`,
**When** enabled,
**Then** pulse animations are suppressed; values update without animation

**Given** the player is in flight,
**When** the HUD renders location context,
**Then** it displays sector identifier plus coarse in-sector position (subsector-style bucket and rounded local coordinates), not meter-level precision

**Given** location context values update during movement,
**When** the player crosses a coarse boundary,
**Then** the location display updates without pulse animation and without `aria-live` announcements

---

### Story 2.2a: Sparse-Space Motion Reference Background

As a **player**,
I want a very faint background motion reference while flying in sparse space,
So that I can perceive ship travel direction and drift even when no asteroids are visible.

**Acceptance Criteria:**

**Given** open-space flight with no nearby visible asteroids,
**When** rendering updates,
**Then** a faint non-intrusive motion-reference layer remains visible in the background

**Given** evaluation mode,
**When** candidate treatments are compared,
**Then** both a faint starfield and a faint sector-aligned grid are available for side-by-side review

**Given** final MVP selection,
**When** evaluation completes,
**Then** one treatment is selected as default and the non-selected treatment is removed from MVP scope

**Given** HUD/context overlays are present,
**When** the motion-reference layer renders,
**Then** HUD readability and interaction clarity are not degraded

**Given** `prefers-reduced-motion`,
**When** enabled,
**Then** the motion-reference layer renders static or minimally animated

**Planning Note:**
- Selected MVP default treatment is **sector-aligned grid**.
- `starfield` is retained only as a non-default explicit evaluation/debug override path.

---

### Story 2.3: Context Panel — Tap-to-Select with Live Range Updates

As a **player**,
I want to tap any object in the game world and see a context panel showing available actions that update live as I fly closer,
So that I always know what I can do with any object at my current distance — without ever needing to re-tap.

**Acceptance Criteria:**

**Given** the player taps/clicks any interactive object on the canvas,
**When** the tap registers,
**Then** the context panel slides in from the right edge with the object's name, type icon, distance indicator, and distance-appropriate actions

**Given** the player is at far range (object visible but out of scan range),
**When** the panel is open,
**Then** only `[Set Course]` and object info are shown; a "Get closer for more" footer is visible

**Given** the player flies to scan range without re-tapping,
**When** the scan range threshold is crossed,
**Then** `[Scan]` appears in the panel via fade-in transition; no re-tap required

**Given** the player flies to mining range,
**When** the mining range threshold is crossed,
**Then** `[Mine]`, `[Drill]`, and/or `[Claim]` appear (context-dependent); the "Get closer for more" footer disappears

**Given** the player moves back out of range mid-interaction,
**When** a range gate is lost,
**Then** the corresponding action greys out (not disappears) in the panel

**Given** the panel is open,
**When** the player taps outside the panel or swipes right,
**Then** the panel dismisses

**Given** the panel on keyboard navigation,
**When** open,
**Then** focus is trapped within the panel; Escape dismisses it (`role="complementary"`, `aria-label="[object name] actions"`)

**Given** all panel touch targets,
**Then** each action button is at least 44×44px

---

### Story 2.4: Asteroid Scanning

As a **player**,
I want to scan an asteroid to reveal its resource composition,
So that I can make informed decisions about whether it's worth mining.

**Acceptance Criteria:**

**Given** the player is within scan range of an asteroid and the context panel is open,
**When** the player taps `[Scan]`,
**Then** a scan animation plays (progress ring on the asteroid) and the `ScanAsteroid` SignalR method is sent to the server

**Given** the scan completes on the server,
**When** the result is returned,
**Then** the context panel shows the resource composition breakdown (ore types and estimated quantities)

**Given** the scan result,
**When** displayed,
**Then** the scan data is stored in the player's navigation catalogue entry for this asteroid (trajectory + composition, keyed by asteroid ID)

**Given** a previously scanned asteroid,
**When** tapped,
**Then** the context panel shows the cached composition from the catalogue alongside the option to re-scan

**Given** the `navigation_catalogue_entries` table,
**When** a scan result is stored,
**Then** a row is upserted with `(player_id, asteroid_id, trajectory_data, composition_data, recorded_at)` using `snake_case` column names

---

### Story 2.5: Asteroid Mining

As a **player**,
I want to mine a scanned asteroid to extract resources into my cargo hold,
So that I have goods to sell and make progress in the game.

**Acceptance Criteria:**

**Given** the player is within mining range and has scanned the asteroid,
**When** the player taps `[Mine]`,
**Then** mining begins: a visual progress indicator appears on the asteroid and the hold percentage in the HUD begins ticking up

**Given** mining is active,
**When** each resource unit is deposited,
**Then** the HUD hold bar pulses (subtle intensity)

**Given** the cargo hold reaching 100%,
**When** the last unit is deposited,
**Then** the hold bar pulses with emphatic intensity and shifts colour; mining continues but deposits are discarded with a notification

**Given** the asteroid's mineable quantity is exhausted,
**When** the last unit is extracted,
**Then** the asteroid is visually marked as depleted; `[Mine]` is removed from the context panel

**Given** all mining quantity calculations,
**Then** they are computed server-side; the client only receives deposit events and current hold state

---

### Story 2.6: Fuel Management

As a **player**,
I want to manage a finite fuel supply that limits how far I can travel without refuelling,
So that exploration decisions carry weight and NPC stations have clear value.

**Acceptance Criteria:**

**Given** the player's ship has a fuel level (0–100%),
**When** thrust is applied,
**Then** fuel is consumed proportional to thrust magnitude; the fuel level updates in the HUD

**Given** fuel reaching 0%,
**When** the last fuel is consumed,
**Then** thrust becomes unavailable; the ship coasts on existing momentum; a low-fuel warning is shown

**Given** the player docking at an NPC station,
**When** docked,
**Then** fuel can be purchased and the fuel level restored (cost deducted from credits)

**Given** the `ships` table,
**When** updated each tick,
**Then** `fuel_level` is persisted so that it survives logout and server restart

---

### Story 2.7: NPC Station — Dock and Sell Cargo

As a **player**,
I want to dock at an NPC station and sell my cargo for credits,
So that I can complete the core economic loop and make tangible progress.

**Acceptance Criteria:**

**Given** the player is within docking range of an NPC station,
**When** the context panel is open,
**Then** `[Dock]` and `[Trade]` actions appear

**Given** the player taps `[Dock]`,
**When** docking completes,
**Then** the player is in docked state; the station's trade panel opens showing held cargo and NPC buy prices

**Given** the NPC pricing model (`NpcPricing.cs`),
**When** the player views buy prices,
**Then** prices reflect dynamic NPC pricing (serving as an economic floor — NPC always buys at base price; market demand adjusts above it)

**Given** the player taps `[Sell All]` or sells individual resource stacks,
**When** the transaction processes,
**Then** credits are added to the player's balance; the hold is reduced accordingly; the credits display pulses

**Given** the sell transaction,
**When** complete,
**Then** the `credits` field in the `players` table and `cargo` in `ships` are updated atomically

---

### Story 2.8: Starter Ship Floor — Free Replacement on Loss

As a **player**,
I want to always have access to a free starter ship if mine is destroyed,
So that losing a ship is a setback, not a catastrophe — and I can always keep playing.

**Acceptance Criteria:**

**Given** the player's ship collides with an asteroid at lethal velocity,
**When** the collision is resolved by the server,
**Then** the ship entity is destroyed; a wreck entity is created at the destruction location (for future salvage)

**Given** the ship is destroyed,
**When** the player's state is updated,
**Then** the player is assigned a free starter ship at the nearest NPC station; cargo held at time of destruction is lost; credits and navigation catalogue are preserved

**Given** the starter ship assignment,
**When** the player reconnects,
**Then** they spawn in the starter ship at the station with a notification: *"Your ship was destroyed. A replacement has been provided at [station name]."*

**Given** the starter ship,
**Then** it is always available regardless of credits — the floor cannot be removed by in-game economy

---

## Epic 3: The Navigation Catalogue & Hyperspace Jumps

A player can build a personal per-asteroid navigation catalogue through scanning, view data density across sectors on a Star Map, buy and sell survey data on the information marketplace, and execute hyperspace jumps to other sectors with the full materialisation beat. Jump risk is readable at a glance — never a safety verdict.

### Story 3.1: Multi-Sector World — Second Sector & Basic Routing

As a **player**,
I want the game world to contain multiple sectors,
So that there is somewhere to jump to, and the concept of sector-to-sector travel is established.

**Acceptance Criteria:**

**Given** the world is initialised,
**When** the server starts,
**Then** at least two sectors exist, each procedurally generated and owned by the same shard (intra-shard multi-sector)

**Given** the gateway's `RegionRegistry`,
**When** a player connects,
**Then** the registry correctly maps each sector to its owning shard using the `regions` table in PostgreSQL + `IMemoryCache` on the gateway

**Given** the `regions` table,
**When** created,
**Then** it uses `snake_case` column names and stores `(region_id, shard_endpoint, sector_ids, created_at)`

---

### Story 3.2: Navigation Catalogue — Store & View Per-Asteroid Survey Data

As a **player**,
I want my scans to be stored in a personal navigation catalogue keyed by asteroid ID, and to view my catalogue's coverage before planning a jump,
So that my exploration has lasting value and informs my decisions.

**Acceptance Criteria:**

**Given** a completed scan (from Story 2.4),
**When** the scan result is received,
**Then** an entry is upserted in `navigation_catalogue_entries` with `(player_id, asteroid_id, trajectory_data, composition_data, recorded_at)`

**Given** a new scan of a previously catalogued asteroid,
**When** the new result is stored,
**Then** the existing entry for that `asteroid_id` is overwritten (not duplicated) — most recent data wins (FR28)

**Given** `GET /api/v1/navigation-catalogue`,
**When** called with a valid JWT,
**Then** the full catalogue for the authenticated player is returned as `{ items: [...], total: N }`

**Given** the catalogue entries,
**When** an asteroid's trajectory has changed since the entry was recorded (due to a collision),
**Then** the entry is flagged with `dataChanged: true` in the response

---

### Story 3.3: Star Map — Sector Coverage Visualisation

As a **player**,
I want to open a full-screen star map showing all known sectors with data density colour-coding from my navigation catalogue,
So that I can assess my coverage before deciding where to jump.

**Acceptance Criteria:**

**Given** the player opens the Star Map (HUD icon or `[Plot Course]` in context panel),
**When** it renders,
**Then** a full-screen zoomable sector grid appears over a dimmed canvas; the player's current position is marked

**Given** each sector tile,
**When** rendered,
**Then** it is coloured by data density token based on the player's catalogue coverage:
`--data-dense` (many recent entries → bright teal fill) /
`--data-sparse` (few or old entries → faded grey fill) /
`--data-changed` (entry contradicted → amber border pulse) /
`--data-absent` (no entries → canvas colour only — void speaks)

**Given** a sector tile,
**When** tapped/clicked,
**Then** a jump confirmation panel appears showing: sector code, entry count, survey age, and `[Jump]` button — no safety rating shown (FR5)

**Given** the star map,
**When** pinch/scroll zoom is used,
**Then** sectors zoom smoothly; sector codes shown as labels, not primary navigation

**Given** keyboard navigation on the star map,
**When** arrow keys are used,
**Then** sector selection moves accordingly; Enter selects; screen reader announces sector code + data status on focus

**Given** the Data Density Indicator,
**When** rendered anywhere,
**Then** it uses density + shape alongside colour — never colour-only encoding (WCAG)

---

### Story 3.4: Hyperspace Jump — Initiation & Transition

As a **player**,
I want to initiate a hyperspace jump to a target sector from the Star Map,
So that I can travel to other parts of the belt and expand my exploration.

**Acceptance Criteria:**

**Given** the jump confirmation panel is open for a target sector,
**When** the player taps `[Jump]`,
**Then** the jump sequence begins: the ship accelerates on canvas and the screen compresses into the jump transition animation (~1–2 seconds — a felt experience, not a loading screen)

**Given** the jump transition,
**When** playing,
**Then** a brief void state is shown — no information, no control; the player is committed

**Given** the `InitiateJump` SignalR method called by the client,
**When** received by the server,
**Then** the server validates the jump (ship not docked, fuel sufficient, target sector exists) and executes the teleport within the shard

**Given** an invalid jump condition,
**When** attempted,
**Then** an error is returned and the jump does not execute; the Star Map remains open with an explanatory message

**Given** a successful jump,
**When** executed,
**Then** a configurable fuel amount is deducted from the ship's fuel level (FR4 — fuel cost as part of arrival window calculation)

---

### Story 3.5: Materialisation Beat — Hyperspace Arrival & Session Re-Entry

As a **player**,
I want to experience a shared arrival sequence when jumping into a new sector or logging back in,
So that every arrival feels like a meaningful moment — not a loading screen.

**Acceptance Criteria:**

**Given** a successful hyperspace arrival or session re-entry,
**When** triggered,
**Then** the Materialisation Screen plays: black void → sector resolves progressively from centre outward → 2–3 second quiet scan window → hazards clarify → bottom HUD bar fades in

**Given** the materialisation sequence,
**When** playing,
**Then** it is not dismissable — plays to completion (~2–3 seconds)

**Given** the scan window,
**When** active,
**Then** minimal UI is shown; ambient audio only; no player interaction possible

**Given** control restored,
**When** the sequence ends,
**Then** `aria-live="assertive"` announces "Arrived in [sector code]" or "Returning to [sector code]"

**Given** a session re-entry where the ship was nudged,
**When** the sequence ends,
**Then** the nudge notification slides in after control is restored

**Given** `prefers-reduced-motion`,
**When** enabled,
**Then** the resolve-from-centre animation is replaced by an instant fade; the quiet window still plays

---

### Story 3.6: Information Marketplace — Browse & Buy Survey Data

As a **player**,
I want to browse sector survey data listings, compare them against my own catalogue, and purchase data that downloads instantly into my navigation catalogue,
So that I can improve my jump coverage before entering an unfamiliar sector.

**Acceptance Criteria:**

**Given** the player opens the Information Marketplace (from a station or HUD),
**When** it renders,
**Then** a scrollable list of survey data listings appears, each as a Catalogue Listing Card showing: sector code, Data Density Indicator badge, entry count, survey age, and price — no quality label, no star ratings

**Given** a listing the player already has newer data for,
**When** displayed,
**Then** it shows state `already-owned`

**Given** the player taps `[Buy]` on a listing,
**When** the purchase is confirmed (Radix `Dialog` confirmation),
**Then** credits are deducted, the survey data is delivered instantly to the player's navigation catalogue (FR19), and the Star Map data density updates in real time

**Given** the purchase transaction,
**When** complete,
**Then** it completes within 2 seconds under normal load (NFR5)

**Given** `GET /api/v1/marketplace/catalogue-listings`,
**When** called,
**Then** it returns paginated listings: `{ items: [...], total: N }` with `camelCase` JSON fields

---

### Story 3.7: Sell Survey Data — List Catalogue Entries for Sale

As a **player**,
I want to package my scanned asteroid catalogue entries and list them for sale on the information marketplace,
So that my survey work has economic value beyond personal use.

**Acceptance Criteria:**

**Given** the player has catalogue entries for a sector,
**When** they open the Information Marketplace and select "My Listings",
**Then** they can see their existing catalogue entries and create a new listing with a set price

**Given** a new listing submitted via `POST /api/v1/marketplace/catalogue-listings`,
**When** created,
**Then** the listing is immediately live and visible to other players

**Given** another player purchases the listing,
**When** the transaction completes,
**Then** credits pulse into the seller's balance and the seller receives a sale notification

**Given** the data delivery to the buyer,
**When** purchased,
**Then** the buyer's catalogue is updated instantly; existing entries are overwritten only where the seller's data is newer (FR28 applies to overwrites)

---

## Epic 4: A Distributed World — Multi-Shard & Auto-Scaling

The game world transparently spans multiple server shards. Players traverse sector boundaries without interruption. The world auto-splits under load and coalesces when quiet. All state survives server restarts. Rolling restarts proceed without mass disconnection.

### Story 4.0: Production Infrastructure & CD Pipeline

As a **developer/ops**,
I want complete production-ready Kubernetes manifests and a working CD pipeline,
So that the application can be deployed to DigitalOcean Kubernetes and all Epic 4 stories have a real cluster to verify their infrastructure ACs against.

**Acceptance Criteria:**

**Given** a merge to `main`,
**When** the GitHub Actions deploy workflow runs,
**Then** Docker images for gateway, shard, and admin-api are built, pushed to `registry.digitalocean.com/belterlife/`, and all K8s manifests are applied to DOKS

**Given** the shard `Deployment` manifest,
**When** inspected,
**Then** it has `strategy.type: RollingUpdate` with `maxUnavailable: 0`, `maxSurge: 1`, a `lifecycle.preStop` exec hook, and `terminationGracePeriodSeconds: 60`

**Given** all three `Deployment` manifests,
**When** applied,
**Then** every container has `resources.requests` and `resources.limits` defined

**Given** the gateway `Service`,
**When** applied,
**Then** `type: LoadBalancer` makes the gateway publicly reachable; admin-api `Service` remains `ClusterIP` (NFR13)

**Given** `infra/k8s/shard/pdb.yaml`,
**When** a voluntary disruption occurs,
**Then** at least one shard pod remains available throughout

**Given** `POST /internal/drain` on the shard,
**When** called,
**Then** it returns HTTP 200 (stub — real drain logic deferred to Story 4.5)

---

### Story 4.1: Entity Handoff Protocol — Design & Validation

As a **developer**,
I want a fully specified and tested entity handoff protocol between shards,
So that all future shard-crossing features are built on a validated, reliable foundation.

**Acceptance Criteria:**

**Given** the `HandoffService.cs` implementation,
**When** a player ship crosses a sector boundary owned by a different shard,
**Then** the source shard sends the full entity state to the destination shard endpoint via `POST /internal/handoff` with the `X-Shard-Secret` header

**Given** the destination shard receives a handoff request,
**When** it acknowledges with HTTP 200,
**Then** the source shard marks the entity as transferred and the gateway updates the player's shard routing

**Given** the destination shard fails to acknowledge within the timeout,
**When** the timeout elapses,
**Then** the handoff is aborted; the entity remains on the source shard; no state is lost

**Given** all `ShardClient` outgoing HTTP calls,
**Then** every request attaches the `X-Shard-Secret` header (injected from K8s Secret env var)

**Given** all shard HTTP pipelines receiving internal requests,
**Then** the `X-Shard-Secret` is validated before any handoff request is processed; missing or invalid secret returns HTTP 401

**Given** unit tests for `HandoffService`,
**When** run,
**Then** both successful handoff and timeout/abort scenarios are covered

---

### Story 4.2: Cross-Sector Player Traversal

As a **player**,
I want to fly or jump between sectors owned by different shards without any observable interruption,
So that the multi-shard architecture is completely invisible to me.

**Acceptance Criteria:**

**Given** a player flying toward a sector boundary owned by a different shard,
**When** the boundary is crossed,
**Then** the entity handoff executes and the player continues without a visible pause or disconnection (NFR17)

**Given** a hyperspace jump to a sector on a different shard,
**When** the jump executes,
**Then** the materialisation beat plays and the player arrives on the new shard with full state intact

**Given** a shard handoff,
**When** it occurs,
**Then** the gateway sends a `PlayerRedirect` message; the client reconnects to the new shard endpoint without any user-visible loading state

---

### Story 4.3: Automatic Shard Splitting Under Load

As a **player**,
I want the game world to handle large concentrations of players by transparently splitting regions,
So that performance is maintained regardless of where players congregate.

**Acceptance Criteria:**

**Given** a shard's simulation load exceeds the configured threshold,
**When** detected,
**Then** `RegionSplitter.cs` divides the region into two sub-regions; entities are bulk-transferred to the new shard using push + confirmation pattern (FR41)

**Given** the bulk transfer,
**When** executing,
**Then** the source shard waits for the destination's acknowledgement before redirecting players

**Given** the split completes,
**When** confirmed,
**Then** the `regions` table is updated; gateway `IMemoryCache` is invalidated; new connections route correctly

**Given** players in the splitting region,
**When** the split executes,
**Then** they experience at most a brief `PlayerRedirect` reconnect — no data loss, no forced logout (NFR9, NFR17)

---

### Story 4.4: Automatic Shard Coalescing Under Low Load

As a **developer (ops)**,
I want adjacent under-loaded shards to automatically merge their regions onto a single shard,
So that infrastructure costs are minimised during quiet periods.

**Acceptance Criteria:**

**Given** adjacent shards both below the coalesce threshold sustained for a configurable period,
**When** coalescing is triggered,
**Then** `RegionCoalescer.cs` bulk-transfers all entities from the retiring shard to the surviving shard (FR42)

**Given** the coalesce transfer completes,
**When** confirmed,
**Then** the `regions` table is updated; the retiring pod is terminated; the gateway cache is invalidated

**Given** players on the retiring shard,
**When** the coalesce executes,
**Then** they are redirected via `PlayerRedirect` — no data loss, no forced logout

---

### Story 4.5: State Durability — Survive Server Restart

As a **player**,
I want all my progress and the game world state to be fully preserved across a server restart,
So that server maintenance never causes data loss.

**Acceptance Criteria:**

**Given** a shard is running with active players and asteroid state,
**When** the shard process restarts (graceful or crash),
**Then** all player state (position, credits, cargo, catalogue, fleet) is fully recoverable from PostgreSQL without manual intervention (NFR6, NFR8)

**Given** the `preStop` K8s lifecycle hook,
**When** triggered before SIGTERM,
**Then** the shard drains active connections gracefully via `PlayerRedirect` before shutdown

**Given** a shard that crashed ungracefully,
**When** it restarts,
**Then** it reloads its region's entity state from PostgreSQL and resumes the simulation loop without manual data repair (NFR8)

**Given** a single shard failing,
**When** it occurs,
**Then** players on other shards are unaffected and experience no data loss (NFR7)

---

### Story 4.6: Rolling Restarts — No Mass Disconnection

As an **administrator**,
I want to restart shards one at a time using Kubernetes rolling update strategy,
So that server maintenance proceeds without simultaneously disconnecting all players.

**Acceptance Criteria:**

**Given** a K8s rolling update is triggered on the shard `Deployment`,
**When** a pod is replaced,
**Then** the `preStop` hook drains the pod; players are redirected to remaining shards; the new pod starts before the next pod is replaced (NFR10)

**Given** the rolling update in progress,
**When** one shard is draining,
**Then** all other shards continue serving their players without interruption

**Given** the gateway region registry cache,
**When** a shard pod is removed or added,
**Then** the `IMemoryCache` is invalidated and routing reflects the updated topology within one cache refresh cycle

---

## Epic 5: Full Economy — Marketplace, Ships, and Territory

Players can trade resources and survey data on a global player marketplace (physical goods require travel to collect), manage a personal fleet, customise ship loadouts, claim asteroids with upkeep, post and fulfil mining rights contracts, place beacons, purchase ship upgrades at stations, and salvage wrecks left by destroyed ships.

### Story 5.1: Global Resource Marketplace — Browse & Buy

As a **player**,
I want to browse resource listings on a global marketplace from anywhere in the belt and purchase goods that I must physically travel to collect,
So that trade creates meaningful spatial decisions and travel is rewarded.

**Acceptance Criteria:**

**Given** `GET /api/v1/marketplace/resource-listings`,
**When** called from any location,
**Then** the full resource marketplace is returned as paginated `{ items: [...], total: N }` with `camelCase` fields (FR16)

**Given** a player purchasing a physical resource from deep space,
**When** the transaction completes,
**Then** credits are deducted but the goods are held at the listing station — not teleported to the buyer (FR18)

**Given** the buyer travels to the listing station and docks,
**When** docked,
**Then** a "Pending collection" notification is shown and the goods can be collected into their cargo hold

**Given** the purchase transaction,
**When** complete,
**Then** it completes within 2 seconds under normal load (NFR5)

**Given** the `resource_listings` table,
**When** created,
**Then** it uses `snake_case` and stores `(listing_id, seller_player_id, resource_type, quantity, price_per_unit, station_id, created_at)`

---

### Story 5.2: List Resources for Sale

As a **player**,
I want to list my mined resources for sale on the global marketplace from any station,
So that I can earn credits from players who want to buy without mining themselves.

**Acceptance Criteria:**

**Given** a player docked at a station with cargo,
**When** they submit `POST /api/v1/marketplace/resource-listings`,
**Then** the listing is live immediately and visible to all players (FR17)

**Given** another player purchases the listing,
**When** the transaction completes,
**Then** credits pulse into the seller's balance; the seller receives a sale notification; goods are held at the originating station for collection

**Given** the seller cancels a listing via `DELETE /api/v1/marketplace/resource-listings/{id}`,
**When** processed,
**Then** the listing is removed and any uncollected goods are returned to the seller's account

---

### Story 5.3: Ship Component Upgrades at NPC Stations

As a **player**,
I want to browse and purchase ship component upgrades at NPC stations,
So that I can improve my ship's capabilities in ways that create meaningful trade-offs.

**Acceptance Criteria:**

**Given** a player docked at an NPC station,
**When** the station trade panel is open,
**Then** a "Components" tab shows available upgrades displayed as card-style comparisons — not stat tables (UX spec)

**Given** a component card,
**When** displayed,
**Then** it shows the trade-off clearly (e.g. "Cargo hold +50% / Speed -10%") (FR23)

**Given** the player purchases a component,
**When** the transaction completes,
**Then** credits are deducted; the component is fitted to the ship; the ship's stats update accordingly (FR21)

**Given** the `ship_components` and `ship_loadouts` tables,
**When** created,
**Then** they use `snake_case` naming and record which components are fitted to each ship

---

### Story 5.4: Fleet Management — Own Multiple Ships

As a **player**,
I want to own and manage a personal fleet of ships stored at stations,
So that I can specialise ships for different roles and switch between them as needed.

**Acceptance Criteria:**

**Given** a player docked at a station,
**When** they access the fleet management panel,
**Then** they can see all ships they own, including current station location and loadout summary (FR22)

**Given** the player selects a ship at the same station,
**When** they switch to it,
**Then** the current ship is stored and the selected ship becomes active

**Given** a ship stored at a different station,
**When** the player attempts to switch to it,
**Then** they are informed the ship is at [station name] and must travel there — no remote retrieval

**Given** the `ships` table,
**When** a new ship is added,
**Then** it is associated with `player_id` and has a `station_id` indicating where it is stored

---

### Story 5.5: Asteroid Claiming & Upkeep

As a **player**,
I want to claim an asteroid and pay ongoing upkeep to maintain exclusive mining rights,
So that I can establish territorial presence and protect valuable finds.

**Acceptance Criteria:**

**Given** a player within mining range of an unclaimed asteroid,
**When** the context panel is open,
**Then** `[Claim]` is available as an action

**Given** the player taps `[Claim]`,
**When** processed,
**Then** the asteroid is claimed; upkeep begins (periodic credit deduction); the asteroid is visually tagged with the player's callsign (FR13)

**Given** the player's credits fall below the upkeep requirement,
**When** upkeep is due,
**Then** the claim lapses after a grace period; the asteroid becomes unclaimed; the player receives a warning notification

**Given** the `asteroid_claims` table,
**When** created,
**Then** it stores `(claim_id, asteroid_id, player_id, claimed_at, upkeep_paid_until)`

---

### Story 5.6: Mining Rights Contracts

As a **player**,
I want to post mining rights contracts on my claimed asteroids and fulfil contracts posted by others,
So that I can monetise claims I can't actively mine, and access claimed resources legitimately.

**Acceptance Criteria:**

**Given** a player who owns a claimed asteroid,
**When** they open the context panel for their claim,
**Then** they can post a mining rights contract specifying terms (resource share % or flat fee, duration) (FR14)

**Given** a contract listing,
**When** posted,
**Then** it appears in the marketplace as a "Mining Rights" listing

**Given** another player who accepts the contract,
**When** accepted,
**Then** they gain temporary mining rights for the contract duration; the owner receives the agreed payment; the context panel shows the active contractor

**Given** the contract duration expires,
**When** elapsed,
**Then** mining rights revert to the owner; the contract is marked complete

---

### Story 5.7: Beacons — Mark Claimed Asteroids

As a **player**,
I want to place a beacon on my claimed asteroid,
So that I can find it on the Star Map and signal my presence to other players.

**Acceptance Criteria:**

**Given** a player who owns a claimed asteroid,
**When** they tap `[Place Beacon]` in the context panel,
**Then** a beacon entity is created at the asteroid's location (FR15)

**Given** a beacon,
**When** visible on the Star Map,
**Then** it appears as a distinct marker on the sector tile, labelled with the owner's callsign

**Given** a beacon within proximity,
**When** another player approaches,
**Then** the context panel shows the beacon owner's callsign and claim status

**Given** the `beacons` table,
**When** created,
**Then** it stores `(beacon_id, asteroid_id, player_id, placed_at)`

---

### Story 5.8: Persistent Wrecks & Salvage

As a **player**,
I want destroyed ships to leave persistent wrecks that I can salvage for resources,
So that destruction has meaning beyond the moment and creates emergent scavenging gameplay.

**Acceptance Criteria:**

**Given** a ship is destroyed,
**When** the server processes the destruction,
**Then** a wreck entity is created at the exact destruction location containing the ship's cargo at time of death (FR25)

**Given** a wreck in the world,
**When** a player flies near it,
**Then** it appears on the canvas as a distinctive visual and the context panel shows `[Salvage]` within interaction range

**Given** the player taps `[Salvage]`,
**When** processed,
**Then** the wreck's contents are transferred to the player's cargo hold (up to hold capacity); the wreck is depleted or removed

**Given** a wreck not salvaged within a configurable time window,
**When** the window elapses,
**Then** the wreck despawns and its contents are lost

**Given** the `wrecks` table,
**When** created,
**Then** it stores `(wreck_id, location_x, location_y, sector_id, cargo_snapshot, created_at)`

---

### Story 5.9: Frontier Outposts — NPC Presence in Deep Space

As a **player**,
I want to discover NPC outposts in frontier sectors far from the starting area,
So that deep-space exploration is rewarded and basic trade/fuel is accessible far from the core belt.

**Acceptance Criteria:**

**Given** a frontier sector (far from starting sectors),
**When** procedurally generated,
**Then** there is a configurable probability of an NPC outpost appearing (smaller and less well-stocked than core stations) (FR10)

**Given** an NPC outpost,
**When** a player docks,
**Then** basic trade (sell resources, buy fuel) is available at a frontier price premium above core station floor

**Given** the outpost on the Star Map,
**When** the sector has been visited or its data purchased,
**Then** the outpost appears as a distinct marker on the sector tile

---

## Epic 6: Administration & Live Service Operations

Administrators can monitor all active shards and player counts, perform rolling restarts without mass disconnection, look up and inspect player accounts, execute a deliberate universe reset as an explicit operation, and trust that all state is preserved across restarts.

### Story 6.1: Admin Authentication & Role Guard

As an **administrator**,
I want to authenticate with an admin account and have all admin endpoints protected behind an `Admin` role,
So that operational tools are never accessible to regular players.

**Acceptance Criteria:**

**Given** `POST /api/v1/auth/login` with admin credentials,
**When** successful,
**Then** the returned JWT contains the `Admin` role claim

**Given** any admin API endpoint,
**When** called without a JWT carrying the `Admin` role,
**Then** HTTP 403 is returned with RFC 9457 Problem Details

**Given** the `BelterLife.Admin` service,
**When** deployed,
**Then** it is only reachable via the K8s cluster-internal network — never publicly exposed (NFR13)

**Given** the ASP.NET Core Identity roles configuration,
**When** set up,
**Then** the `Admin` role is seeded and assignable only via direct database configuration — no self-service promotion

---

### Story 6.2: Shard Status Dashboard

As an **administrator**,
I want to view the status and player count of all active server shards in real time,
So that I can monitor the health of the live service at a glance.

**Acceptance Criteria:**

**Given** `GET /api/v1/admin/shards`,
**When** called with an admin JWT,
**Then** it returns a list of all active shard pods with: shard ID, region(s) owned, current player count, tick rate, and health status (FR36)

**Given** a shard that is unhealthy or unresponsive,
**When** included in the response,
**Then** its status is shown as `degraded` or `unreachable` — not silently omitted

**Given** the admin panel UI,
**When** viewing the shard list,
**Then** it auto-refreshes at a configurable interval without requiring a manual page reload

---

### Story 6.3: Player Account Lookup & Inspection

As an **administrator**,
I want to look up any player account by username and inspect their full game state,
So that I can investigate reports, diagnose issues, and support players.

**Acceptance Criteria:**

**Given** `GET /api/v1/admin/players?username={username}`,
**When** called with an admin JWT,
**Then** the player's account details are returned: username, account created date, current sector, credits, cargo, fleet summary, and navigation catalogue entry count (FR38)

**Given** `GET /api/v1/admin/players/{id}`,
**When** called,
**Then** the full player state is returned including last login timestamp and active shard

**Given** a username that does not exist,
**When** queried,
**Then** HTTP 404 is returned with Problem Details

**Given** all admin player responses,
**Then** they use `camelCase` JSON fields and ISO 8601 UTC timestamps

---

### Story 6.4: Universe Reset — Explicit Admin Operation

As an **administrator**,
I want to trigger a deliberate universe reset that wipes world state while preserving player accounts,
So that I can start a fresh game world without affecting player credentials or the ability to log in.

**Acceptance Criteria:**

**Given** `POST /api/v1/admin/universe/reset`,
**When** called with an admin JWT and a required confirmation token in the request body,
**Then** the universe reset begins: all asteroids, sectors, wrecks, claims, beacons, and contracts are deleted; all ships are returned to starter state at the default station; player credits are reset to the starting balance (FR40)

**Given** the reset operation,
**When** executing,
**Then** player accounts, passwords, and navigation catalogues are preserved — only world and economic state is wiped

**Given** the reset endpoint,
**When** called without the correct confirmation token,
**Then** HTTP 400 is returned — the confirmation token requirement prevents accidental resets

**Given** the reset operation,
**When** complete,
**Then** all shards are notified to reload their world state; players currently connected receive a `ServerMessage` notification: *"A universe reset has been initiated by an administrator. Returning to spawn."*

**Given** the universe reset,
**Then** it is entirely separate from a normal server restart (FR40) — the endpoint is named `/universe/reset`, not `/restart`

---

**Epic 6 Summary — 4 stories (Story 6.1–6.4):**

| Story | FR(s) / NFR(s) covered |
|---|---|
| 6.1 Admin Auth & Role Guard | NFR13 |
| 6.2 Shard Status Dashboard | FR36 |
| 6.3 Player Account Lookup | FR38 |
| 6.4 Universe Reset | FR40 |

*Note: FR37 (rolling restarts) is covered by Story 4.6 in Epic 4 — the K8s RollingUpdate strategy + preStop drain hook is the implementation. No additional story needed here.*

---

## Epic 7: New Player Experience & Contextual Discovery

A new player discovers how to fly, scan, mine, and sell through contextual in-world prompts — no mandatory tutorial screen. The first completed mining cycle (fly → mine → sell → credits) happens within 5 minutes of first login. After 4 contextual prompts the world goes quiet and the contextual UI model takes over.

### Story 7.1: First-Session Contextual Tutorial Tooltips

As a **new player**,
I want to receive 3–4 brief contextual prompts that guide me through my first mining cycle without blocking my view or forcing a tutorial sequence,
So that I understand the core loop through doing, not reading.

**Acceptance Criteria:**

**Given** a player logging in for the very first time,
**When** they spawn in the starting sector,
**Then** Tooltip 1 appears anchored to the nearby highlighted asteroid: *"There is an asteroid nearby. Fly toward it."* (max 12 words)

**Given** the player reaches approach range of the asteroid,
**When** the context panel begins to appear,
**Then** Tooltip 2 appears pointing at the asteroid: *"Tap the asteroid to see what you can do."*

**Given** the player opens the context panel,
**When** the `[Mine]` action is visible,
**Then** Tooltip 3 appears pointing at the Mine button: *"Tap Mine to extract resources."*

**Given** the player taps `[Mine]` and mining begins,
**When** confirmed,
**Then** Tooltip 3 auto-dismisses; an ambient Tooltip 4 appears near the HUD after the hold fills: *"Return to the station to sell your cargo."*; it auto-dismisses after 30 seconds

**Given** Tooltip 4 has dismissed,
**When** any future session begins,
**Then** no tooltips are shown again — the tutorial is permanently complete for this player (stored as `tutorial_completed: true` on the player record)

**Given** each tooltip,
**When** displayed,
**Then** it never blocks the tapped target; it repositions if it would clip the viewport edge

**Given** `prefers-reduced-motion`,
**When** enabled,
**Then** tooltips appear/disappear instantly with no animation

**Given** the Tutorial Tooltip component (Radix `Tooltip` base),
**When** rendered,
**Then** it shows an arrow pointing to the world-space target, prompt text (max 12 words), and auto-advances when the player completes the prompted action

---

### Story 7.2: Contextual Onboarding — World Teaches by Proximity

As a **new player**,
I want the game world itself to surface what I can do through proximity — without any instruction screens, menus, or mandatory onboarding flow after my first session,
So that discovery feels organic and every session after the first is immediately playable.

**Acceptance Criteria:**

**Given** a player on their second or later session,
**When** they log in,
**Then** no tutorial prompts are shown; the materialisation beat plays and they are immediately in control

**Given** any interactive object in the world (asteroid, station, beacon, wreck),
**When** the player approaches,
**Then** a subtle proximity pulse/glow signals interactability — not a forced panel; just an invitation to tap (FR35)

**Given** a player who has never claimed an asteroid,
**When** they scan a valuable asteroid and are within mining range,
**Then** the context panel surfaces `[Claim]` naturally alongside `[Mine]` — no separate tutorial for claiming required

**Given** any new mechanic the player encounters for the first time (claiming, beacons, contracts, hyperspace),
**When** the relevant action first appears in a context panel,
**Then** a single brief inline hint is shown within the panel (e.g. *"Claiming costs upkeep credits"*) — no external tooltip, no blocking modal

**Given** the inline panel hints,
**When** a mechanic has been used at least once,
**Then** the hint is suppressed on subsequent encounters (stored per-player per-mechanic)

---

**Epic 7 Summary — 2 stories (Story 7.1–7.2):**

| Story | FR(s) covered |
|---|---|
| 7.1 First-Session Tutorial Tooltips | FR31 |
| 7.2 World Teaches by Proximity | FR31, FR35 (discovery layer) |

All Epic 7 FRs covered ✅
