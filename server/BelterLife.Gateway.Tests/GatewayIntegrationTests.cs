using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
