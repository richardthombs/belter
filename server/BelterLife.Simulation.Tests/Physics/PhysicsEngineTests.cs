using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Physics;

namespace BelterLife.Simulation.Tests.Physics;

public class PhysicsEngineTests
{
    private static Ship ShipFacingUp() =>
        new() { Id = 1, PlayerId = "p1", SectorId = 1, X = 0, Y = 0, VelocityX = 0, VelocityY = 0, Heading = 0, AngularVelocity = 0 };

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

        // Assert — facing=(sin0,-cos0)=(0,-1); should accelerate upward (VelocityY < 0)
        Assert.Equal(0f, ship.VelocityX, precision: 4);
        Assert.True(ship.VelocityY < 0f, "Should accelerate upward (neg-Y) when facing up");
        Assert.Equal(-PhysicsEngine.ThrustForce * Dt, ship.VelocityY, precision: 3);
        Assert.True(ship.Y < 0, "Ship should have moved upward");
    }

    [Fact]
    public void ApplyPhysics_NoInput_VelocityUnchanged()
    {
        // Pure Newtonian — linear velocity must NOT decay when there is no thrust input.
        var ship = ShipFacingUp();
        ship.VelocityX = 100f;

        _engine.ApplyPhysics(ship, null, Dt);

        Assert.Equal(100f, ship.VelocityX, precision: 4);
        Assert.Equal(0f, ship.VelocityY, precision: 4);
    }

    [Fact]
    public void ApplyPhysics_RetroThrusters_OpposesHeading()
    {
        // Ship facing up, moving upward — retros should reduce upward speed.
        var ship = ShipFacingUp();
        ship.VelocityY = -100f;
        var input = new InputEvent(Thrust: -1, Torque: 0, Brake: false);

        _engine.ApplyPhysics(ship, input, Dt);

        Assert.True(ship.VelocityY > -100f, "Retros should oppose upward motion");
    }

    [Fact]
    public void ApplyPhysics_TorqueRight_AccumulatesAngularVelocity()
    {
        var ship = ShipFacingUp();
        var input = new InputEvent(Thrust: 0, Torque: 1, Brake: false);

        _engine.ApplyPhysics(ship, input, Dt);

        float expectedAV = PhysicsEngine.AngularAccel * Dt;
        Assert.Equal(expectedAV, ship.AngularVelocity, precision: 4);
        Assert.True(ship.Heading > 0f, "Heading should have increased (rotated right)");
    }

    [Fact]
    public void ApplyPhysics_NoTorque_AngularVelocityDecays()
    {
        // Assisted braking for rotation — angular velocity should bleed off.
        var ship = ShipFacingUp();
        ship.AngularVelocity = 2.0f; // already spinning

        _engine.ApplyPhysics(ship, null, Dt);

        Assert.True(ship.AngularVelocity < 2.0f, "Angular velocity should decrease");
        Assert.True(ship.AngularVelocity > 0f, "Should not instantly stop");
    }

    [Fact]
    public void ApplyPhysics_ExceedingMaxSpeed_ClampsToMaxSpeed()
    {
        // Ship facing right (heading = π/2): facing=(1,0). Near MaxSpeed, fire main engines.
        var ship = ShipFacingUp();
        ship.Heading = MathF.PI / 2f;
        ship.VelocityX = PhysicsEngine.MaxSpeed - 1f;
        var input = new InputEvent(Thrust: 1, Torque: 0, Brake: false);

        _engine.ApplyPhysics(ship, input, Dt);

        float speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
        Assert.Equal(PhysicsEngine.MaxSpeed, speed, precision: 2);
    }

    [Fact]
    public void ApplyPhysics_MainEnginesAfterRotation_ThrustFollowsNewHeading()
    {
        // Ship rotated to face right (heading = π/2), then fire main engines.
        var ship = ShipFacingUp();
        ship.Heading = MathF.PI / 2f; // facing=(sin π/2, -cos π/2)=(1, 0)
        var input = new InputEvent(Thrust: 1, Torque: 0, Brake: false);

        _engine.ApplyPhysics(ship, input, Dt);

        Assert.True(ship.VelocityX > 0f, "Should accelerate right");
        Assert.Equal(0f, ship.VelocityY, precision: 3);
    }

    [Fact]
    public void ApplyAsteroidDrift_IntegratesLongPositionFromVelocity()
    {
        var asteroid = new Asteroid
        {
            X = 1_000,
            Y = -2_000,
            VelocityX = 3_000f,
            VelocityY = -1_500f,
        };

        _engine.ApplyAsteroidDrift(asteroid, 0.5f);

        Assert.Equal(2_500, asteroid.X);
        Assert.Equal(-2_750, asteroid.Y);
    }
}

