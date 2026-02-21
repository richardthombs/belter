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
| 1.1 Monorepo scaffold | `.slnx` not `.sln`; SignalR Protocols.MessagePack package; tsconfig noUnusedLocals |
| 1.2 Player auth | `erasableSyntaxOnly` bans TS constructor parameter properties; `CanReadToken()` ≠ safe to parse (wrap ReadJwtToken in try/catch); UserManager test factory needs UserValidator + relaxed IdentityOptions to match production; `dotnet-ef` global tool must be on PATH |
