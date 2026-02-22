using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Entities;
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

	private static IInputBuffer EmptyBuffer()
	{
		var mock = new Mock<IInputBuffer>();
		mock.Setup(b => b.GetAll()).Returns(new Dictionary<string, InputEvent>());
		return mock.Object;
	}

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

		var loop = new SimulationLoop(
			new Mock<IServiceScopeFactory>().Object,
			mockGateway.Object,
			EmptyBuffer(),
			new PhysicsEngine(),
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
			EmptyBuffer(),
			new PhysicsEngine(),
			BuildConfig(),
			NullLogger<SimulationLoop>.Instance);

		// Act — should not throw even on broadcast failure
		var exception = await Record.ExceptionAsync(() => loop.TickAsync(db, CancellationToken.None));

		// Assert
		Assert.Null(exception);
	}

	[Fact]
	public async Task Tick_WithInputBuffer_UpdatesShipPosition()
	{
		// Arrange — ship at origin, input buffer has rightward thrust for player-1
		await using var db = CreateDb();

		var sector = new Sector { Seed = 3 };
		await db.Sectors.AddAsync(sector);
		await db.SaveChangesAsync();

		var ship = new Ship { SectorId = sector.Id, PlayerId = "player-1", X = 0f, Y = 0f, VelocityX = 0f, VelocityY = 0f, Heading = 0f };
		await db.Ships.AddAsync(ship);
		await db.SaveChangesAsync();

		// Stub input buffer returning forward thrust (main engines) for player-1
		var inputMock = new Mock<IInputBuffer>();
		inputMock.Setup(b => b.GetAll()).Returns(new Dictionary<string, InputEvent>
		{
			["player-1"] = new InputEvent(Thrust: 1, Torque: 0, Brake: false),
		});

		var mockGateway = new Mock<IGatewayClient>();
		mockGateway.Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>())).Returns(Task.CompletedTask);

		var loop = new SimulationLoop(
			new Mock<IServiceScopeFactory>().Object,
			mockGateway.Object,
			inputMock.Object,
			new PhysicsEngine(),
			BuildConfig(),
			NullLogger<SimulationLoop>.Instance);

		// Act
		await loop.TickAsync(db, CancellationToken.None);

		// Assert — ship faced up (heading=0); main engines accelerate along (sin0,-cos0)=(0,-1),
		// so Y < 0 (moved upward in screen-space). X stays 0.
		var updated = await db.Ships.FindAsync(ship.Id);
		Assert.NotNull(updated);
		Assert.True(updated!.Y < 0f, "Ship Y should have decreased (moved upward) after forward thrust with heading=0");
		Assert.Equal(0f, updated.X, precision: 3);
	}
}
