using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Physics;

namespace BelterLife.Simulation.Tests.Physics;

public class PhysicsEngineTests
{
	private static Ship RestingShip() =>
		new() { Id = 1, PlayerId = "p1", SectorId = 1, X = 0, Y = 0, VelocityX = 0, VelocityY = 0, Heading = 0 };

	private static float Dt => PhysicsEngine.ThrustForce > 0 ? 0.033f : 0.033f; // 33 ms tick

	private readonly PhysicsEngine _engine = new();

	[Fact]
	public void ApplyPhysics_WithUpThrust_AccumulatesNegativeVelocityY()
	{
		// Arrange — thrust upward (screen-space up = thrustY = -1)
		var ship = RestingShip();
		var input = new InputEvent(ThrustX: 0, ThrustY: -1, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — ship accelerated upward (VelocityY < 0), magnitude ≈ ThrustForce * dt
		Assert.True(ship.VelocityY < 0, "VelocityY should be negative (upward)");
		Assert.Equal(-PhysicsEngine.ThrustForce * Dt, ship.VelocityY, precision: 3);
		Assert.True(ship.Y < 0, "Ship should have moved upward");
	}

	[Fact]
	public void ApplyPhysics_NoThrust_ReducesVelocity()
	{
		// Arrange — ship moving right at speed 100
		var ship = RestingShip();
		ship.VelocityX = 100f;

		// Act — no input (assisted braking)
		_engine.ApplyPhysics(ship, null, Dt);

		// Assert — VelocityX decreased but not instantly zero
		Assert.True(ship.VelocityX < 100f, "VelocityX should have decreased");
		Assert.True(ship.VelocityX > 0f,   "Braking should not instantly stop the ship");
	}

	[Fact]
	public void ApplyPhysics_ThrustInDifferentDirection_VectorAdds()
	{
		// Arrange — ship moving right (vX=100), apply upward thrust
		var ship = RestingShip();
		ship.VelocityX = 100f;
		var input = new InputEvent(ThrustX: 0, ThrustY: -1, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — VelocityX unchanged (no rightward thrust was cancelled), VelocityY went negative
		Assert.Equal(100f, ship.VelocityX, precision: 3);
		Assert.True(ship.VelocityY < 0f, "VelocityY should be negative after upward thrust");
	}

	[Fact]
	public void ApplyPhysics_ExceedingMaxSpeed_ClampsToMaxSpeed()
	{
		// Arrange — ship already near MaxSpeed moving right
		var ship = RestingShip();
		ship.VelocityX = PhysicsEngine.MaxSpeed - 1f;  // 299 units/s

		// Apply rightward thrust enough to exceed MaxSpeed
		var input = new InputEvent(ThrustX: 1, ThrustY: 0, Brake: false);
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — speed clamped to MaxSpeed
		float speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
		Assert.Equal(PhysicsEngine.MaxSpeed, speed, precision: 2);
	}

	[Fact]
	public void ApplyPhysics_RightThrust_HeadingFacesRight()
	{
		// Arrange — thrust right (thrustX=1, thrustY=0)
		var ship = RestingShip();
		var input = new InputEvent(ThrustX: 1, ThrustY: 0, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — heading = Atan2(0,1) - π/2 = -π/2 (PixiJS: ship points right)
		float expected = MathF.Atan2(0f, 1f) - MathF.PI / 2f;
		Assert.Equal(expected, ship.Heading, precision: 4);
	}
}
