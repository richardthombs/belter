using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Shared.Entities;

namespace BelterLife.Simulation.Physics;

/// <summary>
/// Server-authoritative Newtonian physics engine — heading-based flight model.
/// Linear motion: pure Newtonian (no assisted braking) — use retros to stop.
/// Rotation: accumulated angular velocity with assisted damping when no torque input.
/// Ship facing direction at heading θ: (sin θ, −cos θ) in screen-space (PixiJS: 0 = up).
/// NFR12: only InputEvent vectors accepted — no client-submitted position/velocity.
/// </summary>
public class PhysicsEngine
{
    public const float ThrustForce = 150_000f;   // main engine acceleration, mm / s²
    public const float RetroForce = 100_000f;   // retro thruster acceleration, mm / s²
    public const float MaxSpeed = 300_000f;   // speed cap, mm / s
    public const float AngularAccel = 4.0f;   // angular acceleration, rad / s²
    public const float MaxAngularSpeed = 2.5f;   // angular speed cap, rad / s
    public const float AngularDamping = 4.0f;   // rotation braking coefficient, 1/s
    public const float BrakeDamping = 4.0f;   // linear brake damping coefficient, 1/s

    /// <summary>
    /// Applies one physics tick to <paramref name="ship"/>.
    /// Mutates Heading, AngularVelocity, VelocityX/Y, X/Y in place.
    /// </summary>
    public void ApplyPhysics(Ship ship, InputEvent? input, float deltaSeconds)
    {
        float thrust = input?.Thrust ?? 0f;
        float torque = input?.Torque ?? 0f;
        bool brake = input?.Brake ?? false;

        // 1. Rotation — angular velocity with assisted damping.
        if (torque != 0f)
        {
            ship.AngularVelocity += torque * AngularAccel * deltaSeconds;
            // Clamp angular speed.
            ship.AngularVelocity = Math.Clamp(ship.AngularVelocity, -MaxAngularSpeed, MaxAngularSpeed);
        }
        else
        {
            // Assisted braking — angular velocity decays to zero when no torque input.
            float friction = MathF.Max(0f, 1f - AngularDamping * deltaSeconds);
            ship.AngularVelocity *= friction;
        }
        ship.Heading += ship.AngularVelocity * deltaSeconds;

        // 2. Linear thrust — always in ship-facing direction, pure Newtonian (no damping).
        //    PixiJS rotation=0 → nose points up (neg-Y). Facing vector: (sin θ, -cos θ).
        float facingX = MathF.Sin(ship.Heading);
        float facingY = -MathF.Cos(ship.Heading);

        if (thrust > 0f)
        {
            ship.VelocityX += facingX * ThrustForce * deltaSeconds;
            ship.VelocityY += facingY * ThrustForce * deltaSeconds;
        }
        else if (thrust < 0f)
        {
            ship.VelocityX -= facingX * RetroForce * deltaSeconds;
            ship.VelocityY -= facingY * RetroForce * deltaSeconds;
        }
        // No else — zero thrust leaves velocity untouched (pure Newtonian drift).

        // 3. Brake flag — linear damping on demand (manual stop assist).
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
        ship.X += (long)(ship.VelocityX * deltaSeconds);
        ship.Y += (long)(ship.VelocityY * deltaSeconds);
    }
}
