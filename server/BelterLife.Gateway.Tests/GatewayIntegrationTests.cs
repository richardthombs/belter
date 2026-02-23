using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BelterLife.Gateway.Infrastructure;
using BelterLife.Shared.Contracts.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BelterLife.Gateway.Tests;

/// <summary>
/// WebApplicationFactory — replaces PostgreSQL GatewayDbContext with in-memory for integration tests.
/// Environment variables are set in the constructor (before server starts) because
/// AddBelterIdentity reads configuration during Program.cs, before ConfigureWebHost runs.
/// </summary>
public class GatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    readonly string dbName = "GatewayIntegration_" + Guid.NewGuid();

    public GatewayWebApplicationFactory()
    {
        // Must be set BEFORE server starts — AddBelterIdentity validates these at service registration time.
        // ASP.NET Core reads env vars using __ as section separator: Jwt__Key → Jwt:Key
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Host=placeholder");
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-key-32-chars-xx!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "belterlife");
        Environment.SetEnvironmentVariable("Jwt__Audience", "belterlife");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql-configured DbContextOptions<GatewayDbContext>
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GatewayDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // UseInternalServiceProvider bypasses the "multiple providers in one container"
            // check — EF Core uses the explicit service provider instead of scanning the app's.
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<GatewayDbContext>(opt =>
                opt.UseInMemoryDatabase(dbName)
                   .UseInternalServiceProvider(inMemoryProvider));
        });
    }
}

/// <summary>
/// Integration tests for JWT middleware and token revocation.
/// These exercise the full HTTP pipeline — unit tests in AuthControllerTests cover business logic.
/// AC 5: valid JWT grants access to [Authorize] endpoints.
/// AC 6 / NFR11: revoked tokens return 401 on subsequent requests.
/// </summary>
public class AuthIntegrationTests : IClassFixture<GatewayWebApplicationFactory>
{
    readonly GatewayWebApplicationFactory factory;

    public AuthIntegrationTests(GatewayWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    static async Task<string> RegisterAndLogin(HttpClient client, string username)
    {
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { username, password = "securepass" });
        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "securepass" });
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    /// <summary>AC 5 — valid JWT on [Authorize] endpoint grants access (HTTP 204).</summary>
    [Fact]
    public async Task Authorize_WithValidJwt_ReturnsSuccess()
    {
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "ac5_" + Guid.NewGuid().ToString("N")[..8]);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    /// <summary>AC 6 / NFR11 — revoked token returns 401 on subsequent request.</summary>
    [Fact]
    public async Task Logout_RevokedToken_Returns401OnSubsequentRequest()
    {
        var client = factory.CreateClient();
        var token = await RegisterAndLogin(client, "ac6_" + Guid.NewGuid().ToString("N")[..8]);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First logout — revokes the token (204)
        var first = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Second request with the same token — OnTokenValidated finds JTI in revoked_tokens → 401
        var second = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    /// <summary>AC 3 — no JWT on [Authorize] endpoint returns 401.</summary>
    [Fact]
    public async Task Authorize_WithNoJwt_Returns401()
    {
        var client = factory.CreateClient();

        var res = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

/// <summary>
/// Factory variant that additionally replaces IShardClient with a Moq mock,
/// allowing full register → login → spawn integration tests without a live shard.
/// </summary>
public class GatewayWebApplicationFactoryWithMockShard : GatewayWebApplicationFactory
{
    public Mock<IShardClient> ShardMock { get; } = new Mock<IShardClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // apply in-memory DB from base class

        builder.ConfigureServices(services =>
        {
            // Remove the real ShardClient registrations and replace with the mock.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(IShardClient)
                         || d.ServiceType == typeof(ShardClient))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddSingleton<IShardClient>(_ => ShardMock.Object);
        });
    }
}

/// <summary>
/// End-to-end integration tests for the full auth + spawn flow.
///
/// These tests exercise the complete HTTP pipeline:
///   Register → JWT issued (201)
///   Login    → JWT issued (200)
///   Spawn    → shard called with correct player ID, SpawnResponse returned (200)
///
/// The IShardClient is mocked so these tests run without a real shard process.
///
/// Regression coverage: the MapInboundClaims = false fix in IdentitySetup.cs.
/// Without that fix, playerId passed to the shard would be null, causing 502.
/// See docs/jwt-claim-mapping-gotcha.md for full details.
/// </summary>
public class RegisterLoginSpawnIntegrationTests : IDisposable
{
    readonly GatewayWebApplicationFactoryWithMockShard _factory = new();

    public void Dispose() => _factory.Dispose();

    static async Task<(string token, string responseBody)> PostAuth(
        HttpClient client, string path, object body)
    {
        var res = await client.PostAsJsonAsync(path, body);
        var responseBody = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString()! : string.Empty;
        return (token, responseBody);
    }

    /// <summary>Register issues a 201 with a non-empty JWT.</summary>
    [Fact]
    public async Task Register_Returns201_WithToken()
    {
        var client = _factory.CreateClient();
        var username = "reg_" + Guid.NewGuid().ToString("N")[..8];

        var res = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { username, password = "password123" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("token", out var tokenEl));
        Assert.False(string.IsNullOrWhiteSpace(tokenEl.GetString()));
    }

    /// <summary>Login with valid credentials returns 200 with a non-empty JWT.</summary>
    [Fact]
    public async Task Login_WithValidCredentials_Returns200_WithToken()
    {
        var client = _factory.CreateClient();
        var username = "log_" + Guid.NewGuid().ToString("N")[..8];

        // Register first
        await client.PostAsJsonAsync("/api/v1/auth/register", new { username, password = "pass123" });

        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "pass123" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("token", out var tokenEl));
        Assert.False(string.IsNullOrWhiteSpace(tokenEl.GetString()));
    }

    /// <summary>
    /// Full flow: register → login → spawn returns 200 with SpawnResponse.
    ///
    /// Regression: previously returned 502 because MapInboundClaims defaulted to true,
    /// causing User.FindFirstValue("sub") to return null (the claim was remapped to
    /// ClaimTypes.NameIdentifier). The shard received playerId=null and rejected with 400.
    /// </summary>
    [Fact]
    public async Task RegisterLoginSpawn_FullFlow_Returns200WithSpawnResponse()
    {
        var client = _factory.CreateClient();
        var username = "spawn_" + Guid.NewGuid().ToString("N")[..8];
        var expectedSpawn = new SpawnResponse(SectorId: 1, ShipId: 42, SpawnX: 100f, SpawnY: 200f);

        // The mock accepts any non-null playerId and returns the canned SpawnResponse.
        _factory.ShardMock
            .Setup(s => s.SpawnAsync(It.IsNotNull<string>()))
            .ReturnsAsync(expectedSpawn);

        // Register
        var (regToken, _) = await PostAuth(client, "/api/v1/auth/register",
            new { username, password = "spawnpass" });
        Assert.False(string.IsNullOrWhiteSpace(regToken), "Register must return a JWT");

        // Login
        var (loginToken, _) = await PostAuth(client, "/api/v1/auth/login",
            new { username, password = "spawnpass" });
        Assert.False(string.IsNullOrWhiteSpace(loginToken), "Login must return a JWT");

        // Spawn — use the login token
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginToken);
        var spawnRes = await client.PostAsync("/api/v1/players/me/spawn", null);

        Assert.Equal(HttpStatusCode.OK, spawnRes.StatusCode);
        var spawn = await spawnRes.Content.ReadFromJsonAsync<SpawnResponse>();
        Assert.NotNull(spawn);
        Assert.Equal(expectedSpawn.SectorId, spawn.SectorId);
        Assert.Equal(expectedSpawn.ShipId, spawn.ShipId);

        // Verify the shard was called with a real (non-null) player ID.
        _factory.ShardMock.Verify(s => s.SpawnAsync(It.IsNotNull<string>()), Times.Once);
    }
}
