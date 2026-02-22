using BelterLife.Simulation.Entities;

namespace BelterLife.Simulation.Tests.Entities;

public class SectorGeneratorTests
{
    readonly SectorGenerator _generator = new SectorGenerator();

    [Fact]
    public void GenerateSector_AsteroidCount_IsBetween20And50()
    {
        var (_, asteroids, _) = _generator.Generate(12345L);
        Assert.InRange(asteroids.Count, 20, 50);
    }

    [Fact]
    public void GenerateSector_AlwaysHasExactlyOneNpcStation()
    {
        var (_, _, stations) = _generator.Generate(99999L);
        Assert.Single(stations);
    }

    [Fact]
    public void GenerateSector_NpcStation_IsWithin600UnitsOfOrigin()
    {
        var (_, _, stations) = _generator.Generate(42L);
        var station = stations[0];
        var dist = Math.Sqrt(station.X * station.X + station.Y * station.Y);
        Assert.True(dist <= 600.0, $"Station distance {dist} exceeds 600 units");
    }

    [Fact]
    public void GenerateSector_DifferentSeeds_ProduceDifferentLayouts()
    {
        var (_, asteroids1, _) = _generator.Generate(1L);
        var (_, asteroids2, _) = _generator.Generate(2L);
        bool allSame = asteroids1.Count == asteroids2.Count &&
                       asteroids1.Zip(asteroids2).All(p => p.First.X == p.Second.X && p.First.Y == p.Second.Y);
        Assert.False(allSame, "Different seeds should produce different layouts");
    }

    [Fact]
    public void GenerateSector_NoAsteroid_IsWithin100UnitsOfOrigin()
    {
        for (long seed = 0; seed < 20; seed++)
        {
            var (_, asteroids, _) = _generator.Generate(seed);
            foreach (var a in asteroids)
            {
                var dist = Math.Sqrt(a.X * a.X + a.Y * a.Y);
                Assert.True(dist >= 100.0, $"Seed {seed}: asteroid at distance {dist} is within safe zone (< 100 units)");
            }
        }
    }
}
