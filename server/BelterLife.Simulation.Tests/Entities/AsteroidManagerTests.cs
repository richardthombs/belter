using BelterLife.Shared.Entities;
using BelterLife.Simulation.Entities;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Tests.Entities;

public class AsteroidManagerTests
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

    [Fact]
    public async Task UpdateAsync_DoesNotResolveCollisionsAcrossDifferentSectors()
    {
        await using var db = CreateDb();

        var sectorOne = new Sector { Seed = 1 };
        var sectorTwo = new Sector { Seed = 2 };
        await db.Sectors.AddRangeAsync(sectorOne, sectorTwo);
        await db.SaveChangesAsync();

        var first = new Asteroid
        {
            SectorId = sectorOne.Id,
            X = 0,
            Y = 0,
            Radius = 20_000f,
            VelocityX = 70_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        var second = new Asteroid
        {
            SectorId = sectorTwo.Id,
            X = 0,
            Y = 0,
            Radius = 20_000f,
            VelocityX = -70_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        await db.Asteroids.AddRangeAsync(first, second);
        await db.SaveChangesAsync();

        var manager = new AsteroidManager(new PhysicsEngine(), new CollisionResolver());

        await manager.UpdateAsync(db, new[] { sectorOne.Id, sectorTwo.Id }, 0f, CancellationToken.None);
        await db.SaveChangesAsync();

        var asteroids = await db.Asteroids.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, asteroids.Count);
        Assert.All(asteroids, asteroid => Assert.False(asteroid.IsDestroyed));
    }
}
