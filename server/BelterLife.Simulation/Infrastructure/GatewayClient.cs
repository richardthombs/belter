using System.Net.Http.Json;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.Extensions.Logging;

namespace BelterLife.Simulation.Infrastructure;

/// <summary>Typed HttpClient — POSTs WorldStateUpdate ticks to the Gateway's internal broadcast endpoint.</summary>
public class GatewayClient : IGatewayClient
{
    readonly HttpClient _http;
    readonly string _shardSecret;
    readonly ILogger<GatewayClient> _logger;

    public GatewayClient(HttpClient http, IConfiguration config, ILogger<GatewayClient> logger)
    {
        _http = http;
        _shardSecret = config["SHARD_SECRET"] ?? string.Empty;
        _logger = logger;
    }

    public async Task BroadcastAsync(WorldStateUpdate update)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/broadcast")
        {
            Content = JsonContent.Create(update),
        };
        req.Headers.Add("X-Shard-Secret", _shardSecret);

        var response = await _http.SendAsync(req);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gateway returned {StatusCode} on BroadcastAsync for SectorId={SectorId}",
                (int)response.StatusCode, update.SectorId);
            response.EnsureSuccessStatusCode();
        }
    }
}
