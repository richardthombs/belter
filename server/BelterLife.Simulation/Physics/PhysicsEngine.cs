using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;

namespace BelterLife.Simulation.Physics;

/// <summary>
/// Server-authoritative Newtonian physics engine.
/// Applies thrust, assisted braking, speed clamping, and position integration per tick.
/// NFR12: only InputEvent vectors accepted from clients — no client-submitted position/velocity.
/// </summary>
public class PhysicsEngine
{
    public const float ThrustForce  = 150f;  // units / s²
    public const float MaxSpeed     = 300f;  // units / s
    public const float BrakeDamping = 2.0f;  // deceleration coefficient (1/s)

    /// <summary>
    /// Applies one physics tick to the given ship.
    /// Mutates ship.VelocityX/Y, ship.Heading, ship.X/Y in place.
    /// EF change tracking will persist the result when SaveChangesAsync is called.
    /// </summary>
    public void ApplyPhysics(Ship ship, InputEvent? input, float deltaSeconds)
    {
        float tx = input?.ThrustX ?? 0f;
        float ty = input?.ThrustY ?? 0f;
        bool hasThrustInput = tx != 0f || ty != 0f;

        if (hasThrustInput)
        {
            // Normalise thrust vector (diagonal input has same magnitude as cardinal)
            float len = MathF.Sqrt(tx * tx + ty * ty);
            float nx = tx / len;
            float ny = ty / len;

            ship.VelocityX += nx * ThrustForce * deltaSeconds;
            ship.VelocityY += ny * ThrustForce * deltaSeconds;

            // Update heading to face thrust direction.
            // PixiJS rotation: 0 = up (nose of triangle); Atan2(y,x) measures from +X axis.
            // Subtract π/2 to rotate frame so 0 = +Y-up rather than +X-right.
            ship.Heading = MathF.Atan2(ny, nx) - MathF.PI / 2f;
        }
        else
        {
            // Assisted braking — gentle deceleration toward rest when no thrust input.
            // Apply even when input is null (player disconnected briefly).
            float friction = MathF.Max(0f, 1f - BrakeDamping * deltaSeconds);
            ship.VelocityX *= friction;
            ship.VelocityY *= friction;
        }

        // Brake flag from client overrides directional damping with the same friction curve.
        if (input?.Brake == true && hasThrustInput)
        {
            float friction = MathF.Max(0f, 1f - BrakeDamping * deltaSeconds);
            ship.VelocityX *= friction;
            ship.VelocityY *= friction;
        }

        // Clamp to MaxSpeed.
        float speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
        if (speed > MaxSpeed)
        {
            ship.VelocityX = ship.VelocityX / speed * MaxSpeed;
            ship.VelocityY = ship.VelocityY / speed * MaxSpeed;
        }

        // Integrate position.
        ship.X += ship.VelocityX * deltaSeconds;
        ship.Y += ship.VelocityY * deltaSeconds;
    }
}
