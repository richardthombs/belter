namespace BelterLife.Shared.Contracts.Hubs;

/// <summary>
/// Snapshot of ship state broadcast to all clients each tick.
/// X, Y are int64 mm coordinates. Thrust and Torque populated ~once/s for reconciliation.
/// Thrust and Torque are populated approximately once per second for client-side
/// input reconciliation; null on all other ticks.
/// </summary>
public record ShipSnapshot(int ShipId, string PlayerId, long X, long Y, float VelocityX, float VelocityY, float Heading, float? Thrust = null, float? Torque = null);
