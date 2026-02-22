using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Contracts.Hubs;

namespace BelterLife.Gateway.Infrastructure;

public interface IShardClient
{
    Task<SpawnResponse?> SpawnAsync(string playerId);
    Task SendInputAsync(string playerId, InputEvent input);
}
