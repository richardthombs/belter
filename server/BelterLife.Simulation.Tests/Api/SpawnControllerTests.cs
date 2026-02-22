using System.Net;
using System.Net.Http.Json;
using BelterLife.Shared.Contracts.Api;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BelterLife.Simulation.Tests.Api;

/// <summary>
/// WebApplicationFactory for Simulation — replaces PostgreSQL AppDbContext with SQLite in-memory
/// and removes SimulationLoop to prevent the game loop from running during tests.
/// The SQLite connection is held open for the factory lifetime so the in-memory database persists.
/// </summary>
public class SimulationWebApplicationFactory : WebApplicationFactory<Program>
{
    readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SimulationWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Host=placeholder");
        Environment.SetEnvironmentVariable("SHARD_SECRET", "test-shard-secret");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Remove SimulationLoop — prevents game loop from running during tests
            var loopDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(SimulationLoop));
            if (loopDescriptor != null) services.Remove(loopDescriptor);

            // Program.cs skips Npgsql registration in Testing; add SQLite here as the sole provider
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseSqlite(_connection)
                   .UseSnakeCaseNamingConvention());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class SpawnControllerTests : IClassFixture<SimulationWebApplicationFactory>
{
    readonly SimulationWebApplicationFactory _factory;

    public SpawnControllerTests(SimulationWebApplicationFactory factory)
    {
        _factory = factory;
    }

    HttpClient CreateClientWithSecret() =>
        _factory.CreateClient();

    static HttpRequestMessage SpawnRequest(string playerId, string? secret = "test-shard-secret")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/spawn")
        {
            Content = JsonContent.Create(new SpawnRequest(playerId)),
        };
        if (secret != null)
            req.Headers.Add("X-Shard-Secret", secret);
        return req;
    }

    /// <summary>AC 1 — new player spawn returns 201 with SpawnResponse.</summary>
    [Fact]
    public async Task Spawn_NewPlayer_Creates201WithSpawnResponse()
    {
        var client = CreateClientWithSecret();
        var playerId = "player-" + Guid.NewGuid();

        var res = await client.SendAsync(SpawnRequest(playerId));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SpawnResponse>();
        Assert.NotNull(body);
        Assert.True(body.SectorId > 0);
        Assert.True(body.ShipId > 0);
        Assert.Equal(0f, body.SpawnX);
        Assert.Equal(0f, body.SpawnY);
    }

    /// <summary>AC 1 — repeated spawn for same player is idempotent (200 with same IDs).</summary>
    [Fact]
    public async Task Spawn_ExistingPlayer_Returns200WithSameIds()
    {
        var client = CreateClientWithSecret();
        var playerId = "idem-" + Guid.NewGuid();

        var first = await client.SendAsync(SpawnRequest(playerId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<SpawnResponse>();

        var second = await client.SendAsync(SpawnRequest(playerId));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<SpawnResponse>();

        Assert.Equal(firstBody!.SectorId, secondBody!.SectorId);
        Assert.Equal(firstBody.ShipId, secondBody.ShipId);
    }

    /// <summary>AC 1 — missing/wrong X-Shard-Secret returns 403.</summary>
    [Fact]
    public async Task Spawn_MissingShardSecret_Returns403()
    {
        var client = CreateClientWithSecret();
        var res = await client.SendAsync(SpawnRequest("any-player", secret: null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
