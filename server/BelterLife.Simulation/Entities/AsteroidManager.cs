using BelterLife.Shared.Entities;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Entities;

public class AsteroidManager
{
    private readonly PhysicsEngine physicsEngine;
    private readonly CollisionResolver collisionResolver;

    public AsteroidManager(PhysicsEngine physicsEngine, CollisionResolver collisionResolver)
    {
        this.physicsEngine = physicsEngine;
        this.collisionResolver = collisionResolver;
    }

    public async Task<List<Asteroid>> UpdateAsync(
        AppDbContext db,
        IReadOnlyCollection<int> sectorIds,
        float deltaSeconds,
        CancellationToken cancellationToken)
    {
        if (sectorIds.Count == 0)
        {
            return new List<Asteroid>();
        }

        var asteroids = await db.Asteroids
            .Where(a => sectorIds.Contains(a.SectorId) && !a.IsDestroyed)
            .ToListAsync(cancellationToken);

        foreach (var asteroid in asteroids)
        {
            physicsEngine.ApplyAsteroidDrift(asteroid, deltaSeconds);
        }

        var fragments = new List<Asteroid>();
        foreach (var sectorAsteroids in asteroids.GroupBy(a => a.SectorId))
        {
            fragments.AddRange(collisionResolver.ResolveAsteroidCollisions(sectorAsteroids.ToList()));
        }

        if (fragments.Count > 0)
        {
            await db.Asteroids.AddRangeAsync(fragments, cancellationToken);
            asteroids.AddRange(fragments);
        }

        return asteroids;
    }
}
