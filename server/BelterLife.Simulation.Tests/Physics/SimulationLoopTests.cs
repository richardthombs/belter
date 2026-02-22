using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BelterLife.Simulation.Tests.Physics;

public class SimulationLoopTests
{
	private static AppDbContext CreateDb()
	{
		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite("DataSource=:memory:")
			.Options;
		var db = new AppDbContext(options);
		db.Database.OpenConnection();
		db.Database.EnsureCreated();
		return db;
	}

	private static IConfiguration BuildConfig() =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["TickRateMs"] = "33" })
			.Build();

	[Fact]
	public async Task Tick_BroadcastsWorldStateUpdate_ForEachSector()
	{
		// Arrange
		await using var db = CreateDb();

		var sector = new Sector { Seed = 1 };
		await db.Sectors.AddAsync(sector);
		await db.SaveChangesAsync();

		var asteroid = new Asteroid { SectorId = sector.Id, X = 10f, Y = 20f, Radius = 5f, VertexCount = 8, RotationOffset = 0f };
		var ship = new Ship { SectorId = sector.Id, PlayerId = "player-1", X = 1f, Y = 2f, VelocityX = 0f, VelocityY = 0f, Heading = 0f };
		await db.Asteroids.AddAsync(asteroid);
		await db.Ships.AddAsync(ship);
		await db.SaveChangesAsync();

		var mockGateway = new Mock<IGatewayClient>();
		mockGateway.Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>())).Returns(Task.CompletedTask);

		var mockScopeFactory = new Mock<IServiceScopeFactory>();

		var loop = new SimulationLoop(
			mockScopeFactory.Object,
			mockGateway.Object,
			BuildConfig(),
			NullLogger<SimulationLoop>.Instance);

		// Act — call TickAsync directly (internal method exposed via InternalsVisibleTo)
		await loop.TickAsync(db, CancellationToken.None);

		// Assert
		mockGateway.Verify(
			g => g.BroadcastAsync(It.Is<WorldStateUpdate>(u =>
				u.SectorId == sector.Id &&
				u.Ships.Count == 1 &&
				u.Asteroids.Count == 1 &&
				u.Timestamp > 0)),
			Times.Once());
	}

	[Fact]
	public async Task Tick_ContinuesOnBroadcastFailure()
	{
		// Arrange
		await using var db = CreateDb();

		var sector = new Sector { Seed = 2 };
		await db.Sectors.AddAsync(sector);
		await db.SaveChangesAsync();

		var mockGateway = new Mock<IGatewayClient>();
		mockGateway.Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>()))
			.ThrowsAsync(new HttpRequestException("gateway unavailable"));

		var loop = new SimulationLoop(
			new Mock<IServiceScopeFactory>().Object,
			mockGateway.Object,
			BuildConfig(),
			NullLogger<SimulationLoop>.Instance);

		// Act — should not throw even on broadcast failure
		var exception = await Record.ExceptionAsync(() => loop.TickAsync(db, CancellationToken.None));

		// Assert
		Assert.Null(exception);
	}
}
