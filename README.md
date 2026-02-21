# Belter Life

A browser-based 2D MMO asteroid mining and trading game.

## Tech Stack

- **Server:** .NET 10 Worker Service (simulation) + ASP.NET Core (gateway) + PostgreSQL
- **Client:** Vite + TypeScript + PixiJS v8 + SignalR (MessagePack)
- **Infra:** Docker + Kubernetes (DOKS)

## Local Development

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- Docker Desktop

### Setup

1. Copy `.env.example` to `.env` and fill in the values:
   ```bash
   cp .env.example .env
   ```

2. Start the server services (gateway, shard, PostgreSQL):
   ```bash
   docker-compose up
   ```

3. In a separate terminal, start the Vite dev server with HMR:
   ```bash
   cd client
   npm install
   npm run dev
   ```

   > **Note:** The Vite dev server is not included in docker-compose because HMR requires a native Node process.

### Building

Server (from repo root):
```bash
dotnet build server/BelterLife.sln
```

Client (from repo root):
```bash
cd client && npm run build
```

### Running Tests

```bash
dotnet test server/BelterLife.sln
```

## Project Structure

```
belter-life/
├── .github/workflows/   # CI (ci.yml) + CD (deploy.yml)
├── infra/
│   ├── k8s/             # Kubernetes manifests
│   └── docker/          # Dockerfiles
├── server/              # .NET 10 solution
│   ├── BelterLife.Shared/       # Domain contracts (shared)
│   ├── BelterLife.Simulation/   # Physics loop + shard (Worker Service)
│   ├── BelterLife.Gateway/      # Public ASP.NET Core host
│   └── BelterLife.Admin/        # Admin API (internal only)
├── client/              # Vite TypeScript + PixiJS
├── docker-compose.yml   # Local dev: gateway + shard + postgres
└── .env.example
```

## Architecture Notes

- **Server-authoritative physics** — the client never submits positions, only input events
- **`X-Shard-Secret`** header required on all shard-to-shard HTTP calls
- **Naming:** `snake_case` PostgreSQL, `PascalCase` C#, `camelCase` TypeScript & JSON
- See `_bmad-output/planning-artifacts/architecture.md` for the full architecture document
