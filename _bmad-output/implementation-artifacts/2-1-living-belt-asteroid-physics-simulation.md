# Story 2.1: Living Belt — Asteroid Physics Simulation

Status: review

## Story

As a **player**,
I want to see asteroids moving, drifting, and colliding in the belt around me,
so that the world feels alive and dynamic rather than static.

## Acceptance Criteria

1. **Given** the `SimulationLoop.cs` tick,
   **when** running,
   **then** each asteroid's position is updated each tick according to its velocity vector (Newtonian drift).

2. **Given** two asteroids on a collision course,
   **when** they meet,
   **then** collision is detected, both asteroids change trajectory based on mass/momentum, and fragments may be generated (FR8).

3. **Given** a collision that fragments an asteroid,
   **when** it occurs,
   **then** child fragment entities are created in the database with new trajectories and the parent asteroid is marked destroyed.

4. **Given** `AsteroidManager.cs`,
   **when** updating asteroid state,
   **then** all trajectory updates are server-authoritative and reflected in the next `WorldStateUpdate` to all connected clients.

## Tasks / Subtasks

- [x] Task 1 — Extend asteroid domain model and persistence for dynamic simulation (AC: 1, 2, 3)
  - [x] Update `server/BelterLife.Shared/Entities/Asteroid.cs` with drift/collision state fields (`VelocityX`, `VelocityY`, `IsDestroyed`) using existing project naming conventions.
  - [x] Add an EF migration in `server/BelterLife.Simulation/Migrations/` for new asteroid columns; preserve existing snake_case conventions and defaults for backward compatibility.
  - [x] Ensure migration supports existing databases safely (no runtime data fixups in request paths; migration-only policy).

- [x] Task 2 — Seed initial asteroid drift in sector generation (AC: 1)
  - [x] Update `server/BelterLife.Simulation/Entities/SectorGenerator.cs` so generated asteroids receive small initial velocity vectors.
  - [x] Keep all positions in int64 millimetres and velocities in float mm/s.
  - [x] Keep generation bounded by canonical `RegionBounds` constants; do not hardcode alternate sector sizes.

- [x] Task 3 — Implement asteroid drift integration in physics layer (AC: 1)
  - [x] Add asteroid-drift update logic in `server/BelterLife.Simulation/Physics/PhysicsEngine.cs` (or a dedicated method used by asteroid simulation orchestration).
  - [x] Use int64 position integration pattern from Story 2.0 (`(long)(velocity * deltaSeconds)`), avoiding float position fields.

- [x] Task 4 — Implement asteroid collision and fragmentation behavior (AC: 2, 3)
  - [x] Implement/extend collision resolution in `server/BelterLife.Simulation/Physics/CollisionResolver.cs` for asteroid↔asteroid impacts.
  - [x] Apply deterministic/controlled momentum response and bounded fragment generation.
  - [x] Mark destroyed parents and ensure they are excluded from active world snapshots.
  - [x] Create fragment entities with valid sector association and trajectories; persist in same simulation update transaction.

- [x] Task 5 — Add `AsteroidManager` orchestration and wire into tick loop (AC: 1, 2, 3, 4)
  - [x] Create/extend `server/BelterLife.Simulation/Entities/AsteroidManager.cs` to coordinate drift + collision + fragment persistence.
  - [x] Register required services in `server/BelterLife.Simulation/Program.cs` following existing DI style.
  - [x] Integrate asteroid manager invocation into `server/BelterLife.Simulation/Physics/SimulationLoop.cs` before world snapshot broadcast.
  - [x] Ensure only non-destroyed asteroids are broadcast.

- [x] Task 6 — Update contracts and client type compatibility (AC: 4)
  - [x] Update shared hub contract(s) (for example `server/BelterLife.Shared/Contracts/Hubs/AsteroidSnapshot.cs`) if additional asteroid state is needed for rendering.
  - [x] Mirror any contract changes in `client/src/types/index.ts` without changing JSON casing conventions.
  - [x] Keep asteroid position values as int64-origin server fields represented as JS `number` on the client.

- [x] Task 7 — Add/adjust tests for drift, collision response, and fragmentation (AC: 1, 2, 3, 4)
  - [x] Add physics-level unit tests in `server/BelterLife.Simulation.Tests/Physics/` for asteroid drift integration and collision outcomes.
  - [x] Add simulation-loop tests in `server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs` (or adjacent test files) validating server-authoritative asteroid updates and snapshot filtering.
  - [x] Add migration/schema regression checks where appropriate for new asteroid fields.

- [x] Task 8 — Validate build/test gates
  - [x] Run `dotnet build server/BelterLife.slnx`.
  - [x] Run targeted server tests for new/changed simulation files, then run broader `dotnet test server/BelterLife.slnx`.
  - [x] Run `npm run build` in `client/` if shared contracts/types changed.

## Dev Notes

### Story Foundation (Epic 2)

- This story delivers FR8 "living simulation" behavior on top of Story 2.0's int64/mm world model.
- Keep implementation scoped to asteroid movement/collision lifecycle; scanning/mining/economy behavior belongs to later Epic 2 stories.

### Technical Requirements (Must Follow)

- Coordinate model remains unchanged from Story 2.0:
  - Positions: `long` (int64), units in millimetres.
  - Velocities/forces: `float` in mm/s and mm/s².
  - Canonical sector constants: `RegionBounds.SectorSize` and `RegionBounds.HalfSector`.
- Server-authoritative simulation only:
  - Clients never submit asteroid state.
  - Simulation loop computes and persists truth; clients render snapshots.
- Persistence conventions:
  - PostgreSQL identifiers in snake_case.
  - EF Core migrations are source of schema/data evolution.
  - Do not add runtime migration/backfill logic inside API/simulation request paths.

### Architecture Compliance

- Place code in feature-vertical slices:
  - Physics logic in `Simulation/Physics/`.
  - Entity orchestration in `Simulation/Entities/`.
  - Shared message/data contracts in `BelterLife.Shared/Contracts/`.
- Maintain SignalR/REST split:
  - World updates remain in SignalR flow (`WorldStateUpdate`).
  - No new REST endpoints are needed for per-tick asteroid simulation.

### File Structure Requirements

- Expected touched areas:
  - `server/BelterLife.Shared/Entities/Asteroid.cs`
  - `server/BelterLife.Shared/Contracts/Hubs/AsteroidSnapshot.cs` (if payload evolves)
  - `server/BelterLife.Simulation/Physics/PhysicsEngine.cs`
  - `server/BelterLife.Simulation/Physics/CollisionResolver.cs`
  - `server/BelterLife.Simulation/Physics/SimulationLoop.cs`
  - `server/BelterLife.Simulation/Entities/AsteroidManager.cs`
  - `server/BelterLife.Simulation/Program.cs`
  - `server/BelterLife.Simulation/Migrations/*`
  - `server/BelterLife.Simulation.Tests/Physics/*`
  - `client/src/types/index.ts` (only if shared payload shape changes)

### Testing Requirements

- Unit tests should cover:
  - Drift integration moves asteroid by expected int64 delta per tick.
  - Collision detection and momentum response mutate trajectories correctly.
  - Fragmentation creates valid child asteroids and marks parent destroyed.
- Integration/simulation tests should cover:
  - `SimulationLoop` persists asteroid updates each tick.
  - Destroyed asteroids are excluded from outbound snapshots.
  - New fragments are persisted and then included in subsequent snapshots.

### Previous Story Intelligence (Story 2.0)

- Reuse established int64/mm scale everywhere; avoid introducing float-based position logic.
- Use `double` for overlap/distance calculations when squaring large coordinate deltas.
- Legacy coordinate corrections must be handled by migrations (policy documented in `project-context.md`), not runtime request logic.
- Client rendering now applies explicit world→screen scaling; do not "fix" simulation values for visual concerns.

### Git Intelligence Summary

- Recent work is not reliably available in this execution environment via terminal output; rely on current repository state and documented project context.

### Latest Technical Information

- No external web-research step executed in this environment. Use existing pinned stack versions from architecture/project context (`.NET 10`, `EF Core 10`, `PixiJS v8`, SignalR + MessagePack).

### Project Structure Notes

- No structure conflicts detected for this story.
- Keep implementation additive and localized to asteroid simulation components; avoid unrelated refactors.

### References

- `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.1 acceptance criteria)
- `_bmad-output/planning-artifacts/architecture.md` (world model, simulation architecture, conventions)
- `project-context.md` (hard rules, learned gotchas, migration policy)
- `_bmad-output/implementation-artifacts/2-0-int64-coordinate-system-and-scale.md` (previous story learnings)

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- Story context generated from BMAD create-story workflow artifacts.
- dotnet build server/BelterLife.slnx (pass)
- dotnet test server/BelterLife.Simulation.Tests/BelterLife.Simulation.Tests.csproj (pass)
- dotnet test server/BelterLife.slnx (pass)

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added asteroid dynamic state fields (`VelocityX`, `VelocityY`, `IsDestroyed`) and EF migration with backward-compatible defaults.
- Implemented asteroid drift integration, asteroid-to-asteroid momentum response, high-impact fragmentation, and destroyed-parent handling.
- Added `AsteroidManager` orchestration and integrated it into `SimulationLoop` with non-destroyed asteroid snapshot filtering.
- Seeded initial asteroid drift velocities using `RegionBounds` and improved generator inner-safe-radius behavior for spawn consistency.
- Added/updated physics, simulation-loop, and schema tests for drift, collision, fragmentation, persistence, and snapshot behavior.
- No shared hub contract or client type changes were required for this story; existing snapshot payload remains sufficient for ACs.

### File List

- _bmad-output/implementation-artifacts/2-1-living-belt-asteroid-physics-simulation.md
- server/BelterLife.Shared/Entities/Asteroid.cs
- server/BelterLife.Simulation/Entities/AsteroidManager.cs
- server/BelterLife.Simulation/Entities/SectorGenerator.cs
- server/BelterLife.Simulation/Physics/PhysicsEngine.cs
- server/BelterLife.Simulation/Physics/CollisionResolver.cs
- server/BelterLife.Simulation/Physics/SimulationLoop.cs
- server/BelterLife.Simulation/Program.cs
- server/BelterLife.Simulation/Migrations/20260301120000_AddAsteroidDynamicState.cs
- server/BelterLife.Simulation/Migrations/AppDbContextModelSnapshot.cs
- server/BelterLife.Simulation.Tests/Physics/PhysicsEngineTests.cs
- server/BelterLife.Simulation.Tests/Physics/SimulationLoopTests.cs
- server/BelterLife.Simulation.Tests/Physics/CollisionResolverTests.cs
- server/BelterLife.Simulation.Tests/Physics/AsteroidSchemaTests.cs

## Change Log

- 2026-03-01: Implemented Story 2.1 living-belt asteroid simulation (drift, collision, fragmentation, persistence, orchestration, and tests); status moved to `review`.