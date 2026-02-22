namespace BelterLife.Shared.Contracts.Hubs;

/// <summary>
/// Snapshot of ship state broadcast to all clients each tick.
/// Thrust and Torque are populated approximately once per second for client-side
/// input reconciliation; null on all other ticks.
/// </summary>
public record ShipSnapshot(int ShipId, string PlayerId, float X, float Y, float VelocityX, float VelocityY, float Heading, float? Thrust = null, float? Torque = null);
