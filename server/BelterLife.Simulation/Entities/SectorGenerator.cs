using BelterLife.Shared.Entities;
using BelterLife.Simulation.Physics;

namespace BelterLife.Simulation.Entities;

/// <summary>Stateless service that procedurally generates sector content from a seed.</summary>
public class SectorGenerator
{
    public long NewSeed() => new Random().NextInt64();

    public (Sector sector, List<Asteroid> asteroids, List<NpcStation> stations) Generate(long seed, long gridX = 0, long gridY = 0)
    {
        var sector = new Sector
        {
            GridX = gridX,
            GridY = gridY,
            Seed = seed,
            IsGenerated = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var rng = new Random((int)(seed ^ (seed >> 32)));

        var asteroids = new List<Asteroid>();
        int count = rng.Next(20, 51);
        for (int i = 0; i < count; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            float radius = 10_000f + (float)(rng.NextDouble() * 490_000f);
            const float minSafeSurfaceDistance = 500_000f;
            var minCenterDistance = minSafeSurfaceDistance + radius;
            var maxCenterDistance = RegionBounds.HalfSector - radius - 3_000_000f;
            var distanceRange = Math.Max(0f, maxCenterDistance - minCenterDistance);
            double dist = minCenterDistance + (rng.NextDouble() * distanceRange);
            double tangentAngle = angle + (Math.PI / 2);
            float driftSpeed = 500f + (float)(rng.NextDouble() * 15_000f);
            asteroids.Add(new Asteroid
            {
                X = (long)(Math.Cos(angle) * dist),
                Y = (long)(Math.Sin(angle) * dist),
                VelocityX = (float)Math.Cos(tangentAngle) * driftSpeed,
                VelocityY = (float)Math.Sin(tangentAngle) * driftSpeed,
                Radius = radius,
                VertexCount = rng.Next(6, 13),
                RotationOffset = (float)(rng.NextDouble() * Math.PI * 2),
            });
        }

        double stationAngle = rng.NextDouble() * Math.PI * 2;
        double stationDist = 2_000_000 + rng.NextDouble() * 13_000_000;
        var stations = new List<NpcStation>
        {
            new NpcStation
            {
                X = (long)(Math.Cos(stationAngle) * stationDist),
                Y = (long)(Math.Sin(stationAngle) * stationDist),
                Name = "Station Alpha",
            }
        };

        return (sector, asteroids, stations);
    }
}
