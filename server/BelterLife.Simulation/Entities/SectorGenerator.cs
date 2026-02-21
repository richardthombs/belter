using BelterLife.Shared.Entities;

namespace BelterLife.Simulation.Entities;

/// <summary>Stateless service that procedurally generates sector content from a seed.</summary>
public class SectorGenerator
{
    public long NewSeed() => new Random().NextInt64();

    public (Sector sector, List<Asteroid> asteroids, List<NpcStation> stations) Generate(long seed)
    {
        var sector = new Sector { Seed = seed, CreatedAt = DateTimeOffset.UtcNow };
        var rng = new Random((int)(seed ^ (seed >> 32)));

        var asteroids = new List<Asteroid>();
        int count = rng.Next(20, 51);
        for (int i = 0; i < count; i++)
        {
            var angle = rng.NextDouble() * Math.PI * 2;
            var dist = 150 + rng.NextDouble() * 750; // 150–900 units from origin
            asteroids.Add(new Asteroid
            {
                X = (float)(Math.Cos(angle) * dist),
                Y = (float)(Math.Sin(angle) * dist),
                Radius = 5f + (float)(rng.NextDouble() * 35f),
                VertexCount = rng.Next(6, 13),
                RotationOffset = (float)(rng.NextDouble() * Math.PI * 2),
            });
        }

        var stationAngle = rng.NextDouble() * Math.PI * 2;
        var stationDist = 200 + rng.NextDouble() * 400; // 200–600 units from origin
        var stations = new List<NpcStation>
        {
            new NpcStation
            {
                X = (float)(Math.Cos(stationAngle) * stationDist),
                Y = (float)(Math.Sin(stationAngle) * stationDist),
                Name = "Station Alpha",
            }
        };

        return (sector, asteroids, stations);
    }
}
