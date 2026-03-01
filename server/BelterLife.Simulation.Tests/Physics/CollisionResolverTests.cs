using BelterLife.Shared.Entities;
using BelterLife.Simulation.Physics;

namespace BelterLife.Simulation.Tests.Physics;

public class CollisionResolverTests
{
    [Fact]
    public void ResolveAsteroidCollisions_CollisionUpdatesVelocityForBothAsteroids()
    {
        var first = new Asteroid
        {
            Id = 1,
            SectorId = 1,
            X = 0,
            Y = 0,
            Radius = 20_000f,
            VelocityX = 20_000f,
            VelocityY = 0f,
        };
        var second = new Asteroid
        {
            Id = 2,
            SectorId = 1,
            X = 30_000,
            Y = 0,
            Radius = 20_000f,
            VelocityX = -10_000f,
            VelocityY = 0f,
        };

        var resolver = new CollisionResolver();

        var fragments = resolver.ResolveAsteroidCollisions(new List<Asteroid> { first, second });

        Assert.Empty(fragments);
        Assert.True(first.VelocityX < 20_000f);
        Assert.True(second.VelocityX > -10_000f);
        Assert.False(first.IsDestroyed);
        Assert.False(second.IsDestroyed);
    }

    [Fact]
    public void ResolveAsteroidCollisions_HighImpactMarksParentDestroyedAndCreatesFragments()
    {
        var heavy = new Asteroid
        {
            Id = 10,
            SectorId = 7,
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
            Id = 20,
            SectorId = 7,
            X = 10_000,
            Y = 0,
            Radius = 10_000f,
            VelocityX = 70_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 2f,
        };

        var resolver = new CollisionResolver();

        var fragments = resolver.ResolveAsteroidCollisions(new List<Asteroid> { heavy, light });

        Assert.True(light.IsDestroyed);
        Assert.False(heavy.IsDestroyed);
        Assert.Equal(2, fragments.Count);
        Assert.All(fragments, fragment =>
        {
            Assert.Equal(light.SectorId, fragment.SectorId);
            Assert.Equal(light.X, fragment.X);
            Assert.Equal(light.Y, fragment.Y);
            Assert.False(fragment.IsDestroyed);
            Assert.True(fragment.Radius > 0f);
        });
    }

    [Fact]
    public void ResolveAsteroidCollisions_DestroyedParentFragmentsOnlyOncePerTick()
    {
        var first = new Asteroid
        {
            Id = 1,
            SectorId = 9,
            X = 0,
            Y = 0,
            Radius = 10_000f,
            VelocityX = 100_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        var second = new Asteroid
        {
            Id = 2,
            SectorId = 9,
            X = 0,
            Y = 0,
            Radius = 10_000f,
            VelocityX = -100_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        var third = new Asteroid
        {
            Id = 3,
            SectorId = 9,
            X = 0,
            Y = 0,
            Radius = 10_000f,
            VelocityX = 100_000f,
            VelocityY = 0f,
            VertexCount = 8,
            RotationOffset = 0f,
        };

        var resolver = new CollisionResolver();

        var fragments = resolver.ResolveAsteroidCollisions(new List<Asteroid> { first, second, third });

        Assert.True(first.IsDestroyed);
        Assert.Equal(2, fragments.Count);
    }
}
