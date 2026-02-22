using BelterLife.Shared.Contracts.Hubs;

namespace BelterLife.Simulation.Infrastructure;

public interface IGatewayClient
{
    Task BroadcastAsync(WorldStateUpdate update);
}
