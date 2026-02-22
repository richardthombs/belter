using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Physics;

namespace BelterLife.Simulation.Tests.Physics;

public class PhysicsEngineTests
{
	private static Ship ShipFacingUp() =>
		new() { Id = 1, PlayerId = "p1", SectorId = 1, X = 0, Y = 0, VelocityX = 0, VelocityY = 0, Heading = 0 };

	private const float Dt = 0.033f; // 33 ms tick

	private readonly PhysicsEngine _engine = new();

	[Fact]
	public void ApplyPhysics_MainEngines_AcceleratesForwardAlongHeading()
	{
		// Arrange — ship faces up (heading=0), main engines on
		var ship = ShipFacingUp();
		var input = new InputEvent(Thrust: 1, Torque: 0, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — ship accelerated upward (VelocityY < 0); heading=0 facing=(0,-1)
		Assert.Equal(0f, ship.VelocityX, precision: 4);
		Assert.True(ship.VelocityY < 0f, "Should accelerate upward (neg-Y) when facing up");
		Assert.Equal(-PhysicsEngine.ThrustForce * Dt, ship.VelocityY, precision: 3);
		Assert.True(ship.Y < 0f, "Ship should have moved upward");
	}

	[Fact]
	public void ApplyPhysics_RetroThrusters_DeceleratesOppositeToHeading()
	{
		// Arrange — ship faces up (heading=0) moving upward, retro thrusters on
		var ship = ShipFacingUp();
		ship.VelocityY = -100f; // moving up
		var input = new InputEvent(Thrust: -1, Torque: 0, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — retros push backward (facing up → retro pushes down → VelocityY increases toward 0)
		Assert.True(ship.VelocityY > -100f, "Retros should oppose upward motion");
	}

	[Fact]
	public void ApplyPhysics_NoInput_ReducesVelocity()
	{
		// Arrange — ship moving right, no input
		var ship = ShipFacingUp();
		ship.VelocityX = 100f;

		// Act
		_engine.ApplyPhysics(ship, null, Dt);

		// Assert — VelocityX decreased (assisted braking) but not instantly zero
		Assert.True(ship.VelocityX < 100f, "Velocity should decrease");
		Assert.True(ship.VelocityX > 0f, "Braking should not instantly stop the ship");
	}

	[Fact]
	public void ApplyPhysics_ExceedingMaxSpeed_ClampsToMaxSpeed()
	{
		// Arrange — ship near MaxSpeed, facing right (heading = π/2), main engines on
		var ship = ShipFacingUp();
		ship.Heading = MathF.PI / 2f; // facing right
		ship.VelocityX = PhysicsEngine.MaxSpeed - 1f;
		var input = new InputEvent(Thrust: 1, Torque: 0, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — speed clamped to MaxSpeed
		float speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
		Assert.Equal(PhysicsEngine.MaxSpeed, speed, precision: 2);
	}

	[Fact]
	public void ApplyPhysics_TorqueRight_IncreasesHeading()
	{
		// Arrange — ship facing up, rotate right
		var ship = ShipFacingUp();
		var input = new InputEvent(Thrust: 0, Torque: 1, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — heading increased by RotationRate * Dt
		float expected = PhysicsEngine.RotationRate * Dt;
		Assert.Equal(expected, ship.Heading, precision: 4);
	}

	[Fact]
	public void ApplyPhysics_MainEnginesAfterRotation_ThrustFollowsNewHeading()
	{
		// Arrange — ship rotated to face right (heading = π/2), then fire main engines
		var ship = ShipFacingUp();
		ship.Heading = MathF.PI / 2f; // facing right → facing=(sin π/2, -cos π/2)=(1,0)
		var input = new InputEvent(Thrust: 1, Torque: 0, Brake: false);

		// Act
		_engine.ApplyPhysics(ship, input, Dt);

		// Assert — ship accelerated rightward (VelocityX > 0, VelocityY ≈ 0)
		Assert.True(ship.VelocityX > 0f, "Should accelerate right");
		Assert.Equal(0f, ship.VelocityY, precision: 3);
	}
}
