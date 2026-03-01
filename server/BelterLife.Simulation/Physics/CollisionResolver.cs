using BelterLife.Shared.Entities;

namespace BelterLife.Simulation.Physics;

public class CollisionResolver
{
	private const float FragmentationImpactSpeed = 40_000f;
	private const float FragmentSizeRatio = 0.45f;
	private const int FragmentVertexCount = 6;

	public List<Asteroid> ResolveAsteroidCollisions(IList<Asteroid> asteroids)
	{
		var fragments = new List<Asteroid>();

		for (int i = 0; i < asteroids.Count; i++)
		{
			var first = asteroids[i];
			if (first.IsDestroyed)
			{
				continue;
			}

			for (int j = i + 1; j < asteroids.Count && !first.IsDestroyed; j++)
			{
				var second = asteroids[j];
				if (second.IsDestroyed)
				{
					continue;
				}

				if (!AreColliding(first, second))
				{
					continue;
				}

				ResolveMomentum(first, second);

				var impactSpeed = RelativeSpeed(first, second);
				if (impactSpeed < FragmentationImpactSpeed)
				{
					continue;
				}

				var destroyedParent = ChooseDestroyedParent(first, second);
				destroyedParent.IsDestroyed = true;
				fragments.AddRange(CreateFragments(destroyedParent));
				if (ReferenceEquals(destroyedParent, first))
				{
					break;
				}
			}
		}

		return fragments;
	}

	private static bool AreColliding(Asteroid first, Asteroid second)
	{
		var dx = (double)first.X - second.X;
		var dy = (double)first.Y - second.Y;
		var radiusSum = first.Radius + second.Radius;
		return (dx * dx) + (dy * dy) <= (radiusSum * radiusSum);
	}

	private static void ResolveMomentum(Asteroid first, Asteroid second)
	{
		var massFirst = EstimateMass(first);
		var massSecond = EstimateMass(second);

		var v1x = first.VelocityX;
		var v1y = first.VelocityY;
		var v2x = second.VelocityX;
		var v2y = second.VelocityY;

		first.VelocityX = ((massFirst - massSecond) * v1x + (2f * massSecond * v2x)) / (massFirst + massSecond);
		first.VelocityY = ((massFirst - massSecond) * v1y + (2f * massSecond * v2y)) / (massFirst + massSecond);
		second.VelocityX = ((massSecond - massFirst) * v2x + (2f * massFirst * v1x)) / (massFirst + massSecond);
		second.VelocityY = ((massSecond - massFirst) * v2y + (2f * massFirst * v1y)) / (massFirst + massSecond);
	}

	private static float EstimateMass(Asteroid asteroid)
	{
		return MathF.Max(1f, asteroid.Radius * asteroid.Radius);
	}

	private static float RelativeSpeed(Asteroid first, Asteroid second)
	{
		var dx = first.VelocityX - second.VelocityX;
		var dy = first.VelocityY - second.VelocityY;
		return MathF.Sqrt((dx * dx) + (dy * dy));
	}

	private static Asteroid ChooseDestroyedParent(Asteroid first, Asteroid second)
	{
		var massFirst = EstimateMass(first);
		var massSecond = EstimateMass(second);

		if (massFirst < massSecond)
		{
			return first;
		}

		if (massSecond < massFirst)
		{
			return second;
		}

		return first.Id <= second.Id ? first : second;
	}

	private static IEnumerable<Asteroid> CreateFragments(Asteroid parent)
	{
		var childRadius = MathF.Max(2_000f, parent.Radius * FragmentSizeRatio);
		var spread = MathF.Max(1_000f, childRadius * 0.25f);

		yield return CreateFragment(parent, childRadius, -spread, spread);
		yield return CreateFragment(parent, childRadius, spread, -spread);
	}

	private static Asteroid CreateFragment(Asteroid parent, float radius, float velocityXDelta, float velocityYDelta)
	{
		return new Asteroid
		{
			SectorId = parent.SectorId,
			X = parent.X,
			Y = parent.Y,
			VelocityX = parent.VelocityX + velocityXDelta,
			VelocityY = parent.VelocityY + velocityYDelta,
			Radius = radius,
			VertexCount = FragmentVertexCount,
			RotationOffset = parent.RotationOffset,
			IsDestroyed = false,
		};
	}
}
