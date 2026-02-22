using BelterLife.Shared.Contracts.Api;

namespace BelterLife.Gateway.Infrastructure;

public interface IShardClient
{
    Task<SpawnResponse?> SpawnAsync(string playerId);
}
