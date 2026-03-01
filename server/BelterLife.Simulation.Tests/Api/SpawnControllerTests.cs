using System.Net;
using System.Net.Http.Json;
using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Entities;
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
		Assert.Equal(0L, body.SpawnX);
		Assert.Equal(0L, body.SpawnY);
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

	[Fact]
	public async Task Spawn_EmptyPlayerId_Returns400()
	{
		var client = CreateClientWithSecret();
		var res = await client.SendAsync(SpawnRequest("   "));
		Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
	}

	/// <summary>AC 2 — returning player spawn returns actual saved ship position.</summary>
	[Fact]
	public async Task Spawn_ReturningPlayer_ReturnsActualShipPosition()
	{
		var client = CreateClientWithSecret();
		var playerId = "return-" + Guid.NewGuid();

		// First spawn — creates player + ship at (0, 0)
		var first = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.Created, first.StatusCode);
		var firstBody = await first.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(firstBody);

		// Move ship to (250m, 250m) — inside the 500m minimum asteroid distance from generator.
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var ship = await db.Ships.FirstAsync(s => s.Id == firstBody.ShipId);
			ship.X = 250_000L;
			ship.Y = 250_000L;
			await db.SaveChangesAsync();
		}

		// Second spawn — should return actual ship position
		var second = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.OK, second.StatusCode);
		var secondBody = await second.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(secondBody);
		Assert.Equal(250_000L, secondBody.SpawnX);
		Assert.Equal(250_000L, secondBody.SpawnY);
		Assert.False(secondBody.Repositioned);
	}

	/// <summary>AC 3 — returning player is repositioned when ship overlaps an asteroid.</summary>
	[Fact]
	public async Task Spawn_ReturningPlayer_RepositionsShipWhenOverlapsAsteroid()
	{
		var client = CreateClientWithSecret();
		var playerId = "reposition-" + Guid.NewGuid();

		// First spawn — creates player + ship at (0, 0)
		var first = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.Created, first.StatusCode);
		var firstBody = await first.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(firstBody);

		// Add a large asteroid at (0, 0) overlapping the ship
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			db.Asteroids.Add(new Asteroid
			{
				SectorId = firstBody.SectorId,
				X = 0L,
				Y = 0L,
				Radius = 100_000f,
				VertexCount = 6,
				RotationOffset = 0f,
			});
			await db.SaveChangesAsync();
		}

		// Second spawn — should reposition the ship
		var second = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.OK, second.StatusCode);
		var secondBody = await second.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(secondBody);
		Assert.True(secondBody.Repositioned);

		// Verify new position does not overlap the asteroid (radius=100m, margin=10m)
		const double minDist = 100_000.0 + 10_000.0;
		double distSq = Math.Pow(secondBody.SpawnX, 2) + Math.Pow(secondBody.SpawnY, 2);
		Assert.True(distSq >= Math.Pow(minDist, 2),
			$"Ship at ({secondBody.SpawnX}, {secondBody.SpawnY}) still overlaps asteroid");
	}

	[Fact]
	public async Task Spawn_ReturningPlayer_SearchesBeyondInitialMaxSteps_WhenNeeded()
	{
		var client = CreateClientWithSecret();
		var playerId = "extended-search-" + Guid.NewGuid();

		var first = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.Created, first.StatusCode);
		var firstBody = await first.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(firstBody);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			db.Asteroids.Add(new Asteroid
			{
				SectorId = firstBody.SectorId,
				X = 0L,
				Y = 0L,
				Radius = 100_000f,
				VertexCount = 8,
				RotationOffset = 0f,
			});

			for (int step = 1; step <= 10; step++)
			{
				long d = step * 80_000L;
				var candidates = new (long x, long y)[]
				{
					(d, 0L), (-d, 0L), (0L, d), (0L, -d),
					(d, d), (-d, d), (d, -d), (-d, -d),
				};

				foreach (var (x, y) in candidates)
				{
					db.Asteroids.Add(new Asteroid
					{
						SectorId = firstBody.SectorId,
						X = x,
						Y = y,
						Radius = 30_000f,
						VertexCount = 8,
						RotationOffset = 0f,
					});
				}
			}

			await db.SaveChangesAsync();
		}

		var second = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.OK, second.StatusCode);
		var secondBody = await second.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(secondBody);
		Assert.True(secondBody.Repositioned);
		Assert.NotEqual(0L, secondBody.SpawnX);
	}

	/// <summary>AC 4 — new player starts with 500 credits.</summary>
	[Fact]
	public async Task Spawn_NewPlayer_InitialisesCredits_500()
	{
		var client = CreateClientWithSecret();
		var playerId = "credits-" + Guid.NewGuid();

		var res = await client.SendAsync(SpawnRequest(playerId));
		Assert.Equal(HttpStatusCode.Created, res.StatusCode);
		var body = await res.Content.ReadFromJsonAsync<SpawnResponse>();
		Assert.NotNull(body);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var player = await db.Players.FirstAsync(p => p.Id == playerId);
		Assert.Equal(500, player.Credits);
	}
}
