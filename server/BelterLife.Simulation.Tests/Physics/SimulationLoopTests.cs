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

    private static AsteroidManager CreateAsteroidManager()
    {
        return new AsteroidManager(new PhysicsEngine(), new CollisionResolver());
    }

    [Fact]
    public async Task Tick_BroadcastsWorldStateUpdate_ForEachSector()
    {
        // Arrange
        await using var db = CreateDb();

        var sector = new Sector { Seed = 1 };
        await db.Sectors.AddAsync(sector);
        await db.SaveChangesAsync();

        var asteroid = new Asteroid { SectorId = sector.Id, X = 10L, Y = 20L, Radius = 5f, VertexCount = 8, RotationOffset = 0f };
        var ship = new Ship
        {
            SectorId = sector.Id,
            PlayerId = "player-1",
            X = 1L,
            Y = 2L,
            VelocityX = 0f,
            VelocityY = 0f,
            Heading = 0f,
        };
        var player = new Player
        {
            Id = "player-1",
            SectorId = sector.Id,
            ShipId = ship.Id,
            LastSeenAt = DateTimeOffset.UtcNow,
            Credits = 750,
        };
        await db.Asteroids.AddAsync(asteroid);
        await db.Ships.AddAsync(ship);
        await db.Players.AddAsync(player);
        await db.SaveChangesAsync();

        var mockGateway = new Mock<IGatewayClient>();
        mockGateway.Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>())).Returns(Task.CompletedTask);

        var loop = new SimulationLoop(
            new Mock<IServiceScopeFactory>().Object,
            mockGateway.Object,
            EmptyBuffer(),
            new PhysicsEngine(),
            CreateAsteroidManager(),
            BuildConfig(),
            NullLogger<SimulationLoop>.Instance);

        // Act — call TickAsync directly (internal method exposed via InternalsVisibleTo)
        await loop.TickAsync(db, CancellationToken.None);

        // Assert
        mockGateway.Verify(
            g => g.BroadcastAsync(It.Is<WorldStateUpdate>(u =>
                u.SectorId == sector.Id &&
                u.Ships.Count == 1 &&
                u.Ships[0].SectorId == sector.Id &&
                u.Ships[0].Credits == 750 &&
                u.Ships[0].CargoHoldUsed == 0 &&
                u.Ships[0].CargoHoldCapacity == 100 &&
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
            CreateAsteroidManager(),
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

        var ship = new Ship { SectorId = sector.Id, PlayerId = "player-1", X = 0L, Y = 0L, VelocityX = 0f, VelocityY = 0f, Heading = 0f };
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
            CreateAsteroidManager(),
            BuildConfig(),
            NullLogger<SimulationLoop>.Instance);

        // Act
        await loop.TickAsync(db, CancellationToken.None);

        // Assert — ship faced up (heading=0); main engines accelerate along (sin0,-cos0)=(0,-1),
        // so Y < 0 (moved upward in screen-space). X stays 0.
        var updated = await db.Ships.FindAsync(ship.Id);
        Assert.NotNull(updated);
        Assert.True(updated!.Y < 0, "Ship Y should have decreased (moved upward) after forward thrust with heading=0");
        Assert.Equal(0L, updated.X);
    }

    [Fact]
    public async Task Tick_UpdatesAsteroidDrift_ServerAuthoritatively()
    {
        await using var db = CreateDb();

        var sector = new Sector { Seed = 4 };
        await db.Sectors.AddAsync(sector);
        await db.SaveChangesAsync();

        var asteroid = new Asteroid
        {
            SectorId = sector.Id,
            X = 100,
            Y = 200,
            VelocityX = 3_000f,
            VelocityY = -1_500f,
            Radius = 5_000f,
            VertexCount = 7,
        };
        var startX = asteroid.X;
        var startY = asteroid.Y;

        await db.Asteroids.AddAsync(asteroid);
        await db.SaveChangesAsync();

        WorldStateUpdate? captured = null;
        var mockGateway = new Mock<IGatewayClient>();
        mockGateway
            .Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>()))
            .Callback<WorldStateUpdate>(update => captured = update)
            .Returns(Task.CompletedTask);

        var loop = new SimulationLoop(
            new Mock<IServiceScopeFactory>().Object,
            mockGateway.Object,
            EmptyBuffer(),
            new PhysicsEngine(),
            CreateAsteroidManager(),
            BuildConfig(),
            NullLogger<SimulationLoop>.Instance);

        await loop.TickAsync(db, CancellationToken.None);

        var updated = await db.Asteroids.SingleAsync(a => a.Id == asteroid.Id);
        Assert.True(updated.X > startX);
        Assert.True(updated.Y < startY);
        Assert.NotNull(captured);
        Assert.Contains(captured!.Asteroids, a => a.AsteroidId == asteroid.Id);
    }

    [Fact]
    public async Task Tick_ExcludesDestroyedAsteroids_FromWorldSnapshot()
    {
        await using var db = CreateDb();

        var sector = new Sector { Seed = 5 };
        await db.Sectors.AddAsync(sector);
        await db.SaveChangesAsync();

        var heavy = new Asteroid
        {
            SectorId = sector.Id,
            X = 0,
            Y = 0,
            Radius = 30_000f,
            VelocityX = -70_000f,
            VelocityY = 0f,
            VertexCount = 9,
            RotationOffset = 1f,
        };
        var light = new Asteroid
        {
            SectorId = sector.Id,
            X = 10_000,
            Y = 0,
            Radius = 10_000f,
            VelocityX = 70_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 2f,
        };

        await db.Asteroids.AddRangeAsync(heavy, light);
        await db.SaveChangesAsync();

        WorldStateUpdate? captured = null;
        var mockGateway = new Mock<IGatewayClient>();
        mockGateway
            .Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>()))
            .Callback<WorldStateUpdate>(update => captured = update)
            .Returns(Task.CompletedTask);

        var loop = new SimulationLoop(
            new Mock<IServiceScopeFactory>().Object,
            mockGateway.Object,
            EmptyBuffer(),
            new PhysicsEngine(),
            CreateAsteroidManager(),
            BuildConfig(),
            NullLogger<SimulationLoop>.Instance);

        await loop.TickAsync(db, CancellationToken.None);

        var activeCount = await db.Asteroids.CountAsync(a => a.SectorId == sector.Id && !a.IsDestroyed);
        Assert.NotNull(captured);
        Assert.Equal(activeCount, captured!.Asteroids.Count);
    }

    [Fact]
    public async Task Tick_PersistsFragments_AndIncludesThemInSubsequentWorldStateUpdate()
    {
        await using var db = CreateDb();

        var sector = new Sector { Seed = 6 };
        await db.Sectors.AddAsync(sector);
        await db.SaveChangesAsync();

        var heavy = new Asteroid
        {
            SectorId = sector.Id,
            X = 0,
            Y = 0,
            Radius = 3_000f,
            VelocityX = 100_000f,
            VelocityY = 0f,
            VertexCount = 9,
            RotationOffset = 0f,
        };
        var light = new Asteroid
        {
            SectorId = sector.Id,
            X = 10_000,
            Y = 0,
            Radius = 2_000f,
            VelocityX = -100_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        await db.Asteroids.AddRangeAsync(heavy, light);
        await db.SaveChangesAsync();

        var originalIds = new HashSet<int> { heavy.Id, light.Id };

        var broadcasts = new List<WorldStateUpdate>();
        var mockGateway = new Mock<IGatewayClient>();
        mockGateway
            .Setup(g => g.BroadcastAsync(It.IsAny<WorldStateUpdate>()))
            .Callback<WorldStateUpdate>(update => broadcasts.Add(update))
            .Returns(Task.CompletedTask);

        var loop = new SimulationLoop(
            new Mock<IServiceScopeFactory>().Object,
            mockGateway.Object,
            EmptyBuffer(),
            new PhysicsEngine(),
            CreateAsteroidManager(),
            BuildConfig(),
            NullLogger<SimulationLoop>.Instance);

        await loop.TickAsync(db, CancellationToken.None);

        var persistedFragmentIds = await db.Asteroids
            .Where(a => a.SectorId == sector.Id && !a.IsDestroyed && !originalIds.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, persistedFragmentIds.Count);

        await loop.TickAsync(db, CancellationToken.None);

        Assert.True(broadcasts.Count >= 2);
        var secondUpdate = broadcasts[1];
        Assert.All(persistedFragmentIds, fragmentId =>
            Assert.Contains(secondUpdate.Asteroids, snapshot => snapshot.AsteroidId == fragmentId));
    }
}
