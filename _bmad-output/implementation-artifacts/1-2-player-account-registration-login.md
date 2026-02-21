# Story 1.2: Player Account Registration & Login

Status: ready-for-dev

## Story

As a **new player**,
I want to register with a username and password and receive a secure JWT token on login,
So that I have a persistent identity and can access the game.

## Acceptance Criteria

1. **Given** `POST /api/v1/auth/register` with a valid unique username and password,
   **When** the request is submitted,
   **Then** an account is created (password hashed via ASP.NET Core Identity PBKDF2/HMACSHA256) and a JWT token is returned with HTTP 201

2. **Given** `POST /api/v1/auth/login` with valid credentials,
   **When** submitted,
   **Then** a JWT token is returned with HTTP 200

3. **Given** invalid credentials at login,
   **When** submitted,
   **Then** HTTP 401 is returned with RFC 9457 Problem Details body (`application/problem+json`)

4. **Given** a duplicate username at registration,
   **When** submitted,
   **Then** HTTP 400 is returned with Problem Details

5. **Given** a valid JWT token on a `[Authorize]`-protected endpoint,
   **When** the request is made,
   **Then** access is granted (HTTP 200)

6. **Given** `POST /api/v1/auth/logout`,
   **When** called with a valid Bearer token,
   **Then** HTTP 204 is returned and the token's JTI is persisted to `revoked_tokens` table; subsequent requests using that token return HTTP 401 (NFR11)

## Tasks / Subtasks

- [ ] Task 1 — Add `Microsoft.EntityFrameworkCore.Design` to Gateway (AC: all — enables migrations) (AC: 1, 2, 3, 4, 5, 6)
  - [ ] `dotnet add server/BelterLife.Gateway package Microsoft.EntityFrameworkCore.Design` with `PrivateAssets="all"` (dev-only tooling)
  - [ ] Verify `BelterLife.Gateway.csproj` contains the reference

- [ ] Task 2 — Create `RevokedToken` entity and `GatewayDbContext` (AC: 6)
  - [ ] Create `server/BelterLife.Gateway/Infrastructure/RevokedToken.cs` — entity with `Jti` (string, PK), `ExpiresAt` (DateTimeOffset)
  - [ ] Create `server/BelterLife.Gateway/Infrastructure/GatewayDbContext.cs` — `IdentityDbContext<IdentityUser>` subclass
  - [ ] Override `OnModelCreating`: call `base.OnModelCreating(modelBuilder)` first, then `modelBuilder.UseSnakeCaseNamingConvention()`, then configure `RevokedToken` PK
  - [ ] Add `DbSet<RevokedToken> RevokedTokens` property

- [ ] Task 3 — Wire Identity + JWT + DbContext in `GatewayIdentitySetup.cs` and `Program.cs` (AC: 1, 2, 5, 6)
  - [ ] Populate `server/BelterLife.Gateway/Auth/IdentitySetup.cs` with a static `AddBelterIdentity(this IServiceCollection, IConfiguration)` extension method that:
    - Registers `GatewayDbContext` with `UseNpgsql(...).UseSnakeCaseNamingConvention()`
    - Calls `AddIdentity<IdentityUser, IdentityRole>(...).AddEntityFrameworkStores<GatewayDbContext>()`
    - Configures Identity password options: `RequireDigit = false`, `RequireUppercase = false`, `RequireNonAlphanumeric = false`, `MinimumLength = 6`
    - Reads `JwtConfig` section from `IConfiguration` and binds to `JwtConfig` class
    - Registers `JwtTokenService` as scoped
    - Calls `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with token validation params (Issuer, Audience, IssuerSigningKey from `JwtConfig.Key`)
    - Wires JWT query param fallback for SignalR: `OnMessageReceived` event reads `access_token` query param and sets `context.Token`
  - [ ] Update `server/BelterLife.Gateway/Program.cs`:
    - Call `builder.Services.AddBelterIdentity(builder.Configuration)` before `Build()`
    - Add `app.UseAuthentication()` and `app.UseAuthorization()` BEFORE `app.MapControllers()`
    - Ensure order: `UseRouting()` → `UseAuthentication()` → `UseAuthorization()` → `MapControllers()` → `MapHub<GameHub>(...)` → `MapHealthChecks(...)`

- [ ] Task 4 — Create `JwtTokenService` (AC: 1, 2, 6)
  - [ ] Create `server/BelterLife.Gateway/Auth/JwtTokenService.cs`
  - [ ] Constructor takes `IOptions<JwtConfig>`
  - [ ] Method `GenerateToken(IdentityUser user): string` — creates a JWT with:
    - Claims: `sub` = `user.Id`, `name` = `user.UserName`, `jti` = `Guid.NewGuid().ToString()`
    - `exp` = `DateTime.UtcNow.AddHours(24)` (24h expiry)
    - Signs with `HmacSha256` using `JwtConfig.Key`
  - [ ] Method `GetJti(string token): string` — extracts JTI from a token string without full validation (used in logout)

- [ ] Task 5 — Run first EF Core migration (AC: 1, 2, 3, 4, 5, 6)
  - [ ] From repo root: `dotnet ef migrations add InitialIdentitySchema --project server/BelterLife.Gateway --startup-project server/BelterLife.Gateway`
  - [ ] The migration creates: ASP.NET Core Identity tables (`asp_net_users`, `asp_net_roles`, `asp_net_user_roles`, `asp_net_user_claims`, `asp_net_user_logins`, `asp_net_user_tokens`, `asp_net_role_claims`) + `revoked_tokens` — all with `snake_case` names from `UseSnakeCaseNamingConvention()`
  - [ ] Commit the generated `Migrations/` folder files
  - [ ] Do NOT run `dotnet ef database update` — that happens in `Program.cs` or docker-compose startup (see Dev Notes)
  - [ ] Add `app.Services.GetRequiredService<GatewayDbContext>().Database.Migrate()` call at app startup (after `Build()`, before `Run()`) so the schema is auto-applied on container start

- [ ] Task 6 — Implement `AuthController` (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Populate `server/BelterLife.Gateway/Api/v1/AuthController.cs`:
    - Constructor injects: `UserManager<IdentityUser>`, `JwtTokenService`, `GatewayDbContext`
    - `POST /api/v1/auth/register` — `RegisterRequest { Username, Password }`:
      - Create `new IdentityUser { UserName = request.Username }`
      - Call `userManager.CreateAsync(user, password)` — returns 201 + `{ token }` on success, 400 + Problem Details if `!result.Succeeded`
    - `POST /api/v1/auth/login` — `LoginRequest { Username, Password }`:
      - `userManager.FindByNameAsync(username)` + `userManager.CheckPasswordAsync(user, password)`
      - Returns 200 + `{ token }` on success, 401 Problem Details if user not found or wrong password
    - `POST /api/v1/auth/logout` — `[Authorize]`:
      - Extract `jti` and `exp` from current `User.Claims`
      - Persist `new RevokedToken { Jti = jti, ExpiresAt = exp }` to `GatewayDbContext.RevokedTokens`
      - Returns 204 No Content
  - [ ] Add token revocation check middleware or `OnTokenValidated` event in `IdentitySetup.cs`: query `GatewayDbContext` for the incoming JTI in `revoked_tokens`; if found, call `context.Fail("Token revoked")`
  - [ ] Create record types `RegisterRequest` and `LoginRequest` in `server/BelterLife.Gateway/Api/v1/` (or as nested records in the controller file)

- [ ] Task 7 — Update client `RestClient.ts` (AC: 1, 2, 6)
  - [ ] Implement `client/src/network/RestClient.ts` with:
    - `const TOKEN_KEY = 'belter_jwt'`
    - `register(username: string, password: string): Promise<void>` — POST to `/api/v1/auth/register`, stores token in `localStorage`
    - `login(username: string, password: string): Promise<void>` — POST to `/api/v1/auth/login`, stores token in `localStorage`
    - `logout(): Promise<void>` — POST to `/api/v1/auth/logout` with Bearer token, removes token from `localStorage`
    - `getToken(): string | null` — returns `localStorage.getItem(TOKEN_KEY)`
    - All methods throw typed errors on non-2xx responses
  - [ ] Update `client/src/network/GameHubClient.ts` `accessTokenFactory` to call `restClient.getToken()` (import from `RestClient.ts`)
  - [ ] Ensure `npm run build` exits 0 — `noUnusedLocals: true` means all exports must be used or exported (see gotcha in Dev Notes)

- [ ] Task 8 — Write tests (AC: 1, 2, 3, 4, 5, 6)
  - [ ] In `server/BelterLife.Gateway.Tests/`:
    - Add `Microsoft.AspNetCore.Mvc.Testing` package for WebApplicationFactory-style integration tests (or use unit tests with mocked `UserManager`)
    - `AuthControllerTests.cs`:
      - `Register_WithValidCredentials_Returns201WithToken()`
      - `Register_WithDuplicateUsername_Returns400ProblemDetails()`
      - `Login_WithValidCredentials_Returns200WithToken()`
      - `Login_WithInvalidPassword_Returns401ProblemDetails()`
      - `Logout_WithValidToken_Returns204AndRevokesToken()`

- [ ] Task 9 — Verify end-to-end (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Run `dotnet build server/BelterLife.slnx` — expect 0 errors
  - [ ] Run `dotnet test server/BelterLife.slnx` — all tests pass
  - [ ] Run `cd client && npm run build` — expect 0 TypeScript errors

## Dev Notes

### Architecture Guardrails — MUST Follow

- **Gateway has its own `GatewayDbContext : IdentityDbContext<IdentityUser>`** — it connects to the same PostgreSQL instance as Simulation's `AppDbContext` but is a separate context managing different tables. Do NOT merge them.
- **`UseSnakeCaseNamingConvention()` on `GatewayDbContext`** — call it in `OnModelCreating` AFTER `base.OnModelCreating(modelBuilder)`. Identity table names will become `asp_net_users`, `asp_net_roles`, etc. This is correct and expected.
- **This is the FIRST EF Core migration in the project** — the migration goes in `server/BelterLife.Gateway/Migrations/` (not Simulation's Migrations folder). Migration runner is the Gateway project.
- **`app.UseAuthentication()` MUST come before `app.UseAuthorization()`** — swap these and all `[Authorize]` attributes silently pass. This is a notorious ASP.NET Core ordering bug.
- **REST endpoint, not SignalR** — auth is REST only (per architecture: "SignalR = game state + player input; REST = auth"). Do NOT add auth methods to `GameHub`.
- **JWT query param for SignalR** — the `OnMessageReceived` event setup in `IdentitySetup.cs` is critical for Story 1.4 (SignalR with auth). Wire it correctly now even though it's not tested in this story:
  ```csharp
  options.Events = new JwtBearerEvents
  {
      OnMessageReceived = context =>
      {
          var accessToken = context.Request.Query["access_token"];
          var path = context.HttpContext.Request.Path;
          if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
              context.Token = accessToken;
          return Task.CompletedTask;
      }
  };
  ```
- **Token revocation via DB is required (NFR11)** — in-memory revocation does not survive restarts. Use the `revoked_tokens` table. The `OnTokenValidated` event in `JwtBearerEvents` should query the DB for the incoming JTI. This adds one DB query per authenticated request — acceptable for MVP. Use `async` and `await` correctly here.
- **`RevokedToken` table cleanup** — rows expire when the token expires. A background cleanup (delete rows where `expires_at < now`) is out of scope for this story; accumulation is negligible for MVP.
- **Error responses** — use `TypedResults.Problem(...)` (minimal API style) or `Problem(...)` from `ControllerBase`. Response `Content-Type` must be `application/problem+json`. ASP.NET Core does this automatically when using `Problem()`.
- **No `players` game table yet** — the game-specific `players` table (with `last_shard`, `credits`, etc.) comes in Story 1.3. This story only creates Identity tables + `revoked_tokens`.
- **Identity password config** — relax the defaults to `MinimumLength = 6`, no special chars required. Game accounts don't need enterprise password policy.
- **JWT key length** — `JwtConfig.Key` must be ≥ 256 bits (32 bytes) for HMACSHA256. Document this in `.env.example`. The existing `.env.example` has `JWT_KEY=` — the dev agent must NOT hard-code a key in source; read from `IConfiguration`.
- **C# naming** — `PascalCase` for class names, method names, properties. `camelCase` for local vars and private fields. No `_` prefix on fields. This is enforced by the project naming convention.

### NuGet Packages Needed

**Add to `BelterLife.Gateway`:**
```bash
dotnet add server/BelterLife.Gateway package Microsoft.EntityFrameworkCore.Design
```
(Add with `PrivateAssets="all"` in csproj to keep it dev-only.)

**All other required packages already present in `BelterLife.Gateway.csproj`:**
- `Microsoft.AspNetCore.Authentication.JwtBearer` ✅
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` ✅
- `Npgsql.EntityFrameworkCore.PostgreSQL` ✅
- `EFCore.NamingConventions` ✅

**Add to `BelterLife.Gateway.Tests` (for integration tests):**
```bash
dotnet add server/BelterLife.Gateway.Tests package Microsoft.AspNetCore.Mvc.Testing
```

### Migration Commands

Run from repo root:
```bash
# Generate migration
dotnet ef migrations add InitialIdentitySchema \
  --project server/BelterLife.Gateway \
  --startup-project server/BelterLife.Gateway

# Apply migration locally (requires running Postgres)
dotnet ef database update \
  --project server/BelterLife.Gateway \
  --startup-project server/BelterLife.Gateway
```

`dotnet ef` requires `Microsoft.EntityFrameworkCore.Design` on the startup project (which is Gateway — both `--project` and `--startup-project` point to Gateway).

In production, `Database.Migrate()` is called at startup in `Program.cs` after `app = builder.Build()`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    db.Database.Migrate();
}
```

### Token Revocation Pattern

```csharp
// In IdentitySetup.cs AddJwtBearer options:
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        // SignalR WebSocket upgrade uses access_token query param (browser limitation)
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            context.Token = accessToken;
        return Task.CompletedTask;
    },
    OnTokenValidated = async context =>
    {
        var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti is not null)
        {
            var db = context.HttpContext.RequestServices
                           .GetRequiredService<GatewayDbContext>();
            if (await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
                context.Fail("Token has been revoked");
        }
    }
};
```

### Client JWT Storage Pattern

```typescript
// RestClient.ts — store token in localStorage (acceptable for browser game client)
const TOKEN_KEY = 'belter_jwt';

export async function login(username: string, password: string): Promise<void> {
    const res = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
    });
    if (!res.ok) throw new Error(`Login failed: ${res.status}`);
    const { token } = await res.json() as { token: string };
    localStorage.setItem(TOKEN_KEY, token);
}

export function getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
}
```

`GameHubClient.ts` already has `accessTokenFactory: () => localStorage.getItem('belter_jwt') ?? ''` — update it to import and call `getToken()` from `RestClient.ts` instead of duplicating the key name.

### TypeScript `noUnusedLocals` Gotcha

`client/tsconfig.json` has `noUnusedLocals: true`. Every exported function in `RestClient.ts` must be imported somewhere (or the export is sufficient to suppress the error). As long as functions are exported, the build will pass even if not imported in this story.

### Integration Test Pattern

For `Microsoft.AspNetCore.Mvc.Testing`:
- **`InternalsVisibleTo` required** — The `Program` type in ASP.NET Core is internal by default. Add to `BelterLife.Gateway.csproj`:
  ```xml
  <ItemGroup>
    <InternalsVisibleTo Include="BelterLife.Gateway.Tests" />
  </ItemGroup>
  ```
- Create a `WebApplicationFactory<Program>` subclass that overrides `ConfigureWebHost` to use an in-memory or SQLite test database
- OR mock `UserManager<IdentityUser>` and inject via `WebApplicationFactory.WithWebHostBuilder`
- Use `HttpClient` from factory to call endpoints and assert status codes + response bodies
- **Gateway.Tests project needs a `<ProjectReference>` to Gateway** — already there from Story 1.1 ✅

### JWT & Connection String Configuration Naming

.NET `IConfiguration` uses `__` (double underscore) as the section separator in environment variables. The sections map as follows:

| `appsettings.json` key | Environment variable | Used in `.env.example` / docker-compose |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | `ConnectionStrings__Default` |
| `Jwt:Key` | `Jwt__Key` | (currently `JWT_KEY=` in `.env.example` — **wrong format**) |
| `Jwt:Issuer` | `Jwt__Issuer` | not currently in `.env.example` |
| `Jwt:Audience` | `Jwt__Audience` | not currently in `.env.example` |

**Action required:** Update `.env.example` and `docker-compose.yml` to use the correct double-underscore format:
```
Jwt__Key=your-secret-key-at-least-32-chars
Jwt__Issuer=belterlife
Jwt__Audience=belterlife
```

And bind in `IdentitySetup.cs` as:
```csharp
services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
```

**JWT Key length** — must be ≥ 32 bytes (256 bits) for HMACSHA256. Document this in `.env.example` comment:
```
# JWT signing key — must be at least 32 characters
Jwt__Key=
```

### References

- Auth architecture: [Source: architecture.md#Authentication & Security]
- JWT query param for SignalR: [Source: architecture.md#SignalR auth — JWT passed as access_token query parameter]
- Transport split (REST for auth): [Source: architecture.md#API & Communication Patterns]
- NFR11 (session invalidation): [Source: epics.md#NonFunctional Requirements — NFR11]
- Error format (Problem Details): [Source: architecture.md#Error format — RFC 9457 Problem Details]
- Story 1.2 ACs: [Source: epics.md#Story 1.2: Player Account Registration & Login]
- Key file locations: [Source: project-context.md#Key File Locations]
- Naming conventions: [Source: project-context.md#Critical Naming Conventions]
- EFCore.NamingConventions: [Source: project-context.md#Toolchain Gotchas]
- Migration toolchain: [Source: project-context.md#Architecture Rules]
- Story 1.3 context (players table is NOT in this story): [Source: epics.md#Story 1.3]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
