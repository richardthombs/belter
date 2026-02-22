namespace BelterLife.Shared.Contracts.Hubs;

/// <summary>
/// Player input intent — interpreted relative to the ship's current heading.
/// Thrust: 1 = main engines (forward), -1 = retro thrusters (backward), 0 = off.
/// Torque: 1 = rotate right, -1 = rotate left, 0 = no rotation.
/// </summary>
public record InputEvent(float Thrust, float Torque, bool Brake);
