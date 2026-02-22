namespace BelterLife.Gateway.Routing;

/// <summary>
/// "Which shard owns sector X?" — PostgreSQL-backed, gateway-cached via IMemoryCache.
/// Invalidated on every shard split or coalesce.
/// </summary>
public class RegionRegistry
{
}
