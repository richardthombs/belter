using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;

namespace BelterLife.Simulation.Physics;

/// <summary>
/// Server-authoritative Newtonian physics engine — heading-based flight model.
/// Ship facing direction at heading θ: (sin θ, −cos θ) in screen-space (PixiJS: 0 = up).
/// NFR12: only InputEvent vectors accepted — no client-submitted position/velocity.
/// </summary>
public class PhysicsEngine
{
    public const float ThrustForce  = 150f;   // main engine acceleration, units / s²
    public const float RetroForce   = 100f;   // retro thruster acceleration, units / s²
    public const float MaxSpeed     = 300f;   // speed cap, units / s
    public const float BrakeDamping = 2.0f;   // assisted braking coefficient, 1/s
    public const float RotationRate = 2.5f;   // rotation speed, radians / s

    /// <summary>
    /// Applies one physics tick to <paramref name="ship"/>.
    /// Mutates Heading, VelocityX/Y, X/Y in place — EF change tracking persists these.
    /// </summary>
    public void ApplyPhysics(Ship ship, InputEvent? input, float deltaSeconds)
    {
        float thrust = input?.Thrust ?? 0f;
        float torque = input?.Torque ?? 0f;
        bool  brake  = input?.Brake  ?? false;

        // 1. Rotation — apply before thrust so thrust immediately uses new heading.
        if (torque != 0f)
            ship.Heading += torque * RotationRate * deltaSeconds;

        // 2. Thrust — always in ship-facing direction.
        //    PixiJS rotation=0 → nose points up (neg-Y). Facing vector: (sin θ, -cos θ).
        float facingX = MathF.Sin(ship.Heading);
        float facingY = -MathF.Cos(ship.Heading);

        if (thrust > 0f)
        {
            // Main engines — accelerate forward.
            ship.VelocityX += facingX * ThrustForce * deltaSeconds;
            ship.VelocityY += facingY * ThrustForce * deltaSeconds;
        }
        else if (thrust < 0f)
        {
            // Retro thrusters — decelerate by pushing backward.
            ship.VelocityX -= facingX * RetroForce * deltaSeconds;
            ship.VelocityY -= facingY * RetroForce * deltaSeconds;
        }
        else
        {
            // No thrust — assisted braking gradually bleeds off velocity.
            float friction = MathF.Max(0f, 1f - BrakeDamping * deltaSeconds);
            ship.VelocityX *= friction;
            ship.VelocityY *= friction;
        }

        // 3. Brake flag — additional damping on top of existing thrust (e.g., parking brake).
        if (brake)
        {
            float friction = MathF.Max(0f, 1f - BrakeDamping * deltaSeconds);
            ship.VelocityX *= friction;
            ship.VelocityY *= friction;
        }

        // 4. Clamp to MaxSpeed.
        float speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
        if (speed > MaxSpeed)
        {
            ship.VelocityX = ship.VelocityX / speed * MaxSpeed;
            ship.VelocityY = ship.VelocityY / speed * MaxSpeed;
        }

        // 5. Integrate position.
        ship.X += ship.VelocityX * deltaSeconds;
        ship.Y += ship.VelocityY * deltaSeconds;
    }
}
