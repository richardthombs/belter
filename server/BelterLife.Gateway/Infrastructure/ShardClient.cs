using System.Net.Http.Json;
using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.Extensions.Logging;

namespace BelterLife.Gateway.Infrastructure;

/// <summary>Typed HttpClient for communicating with the simulation shard's internal API.</summary>
public class ShardClient : IShardClient
{
    readonly HttpClient _http;
    readonly string _shardSecret;
    readonly ILogger<ShardClient> _logger;

    public ShardClient(HttpClient http, IConfiguration config, ILogger<ShardClient> logger)
    {
        _http = http;
        _shardSecret = config["SHARD_SECRET"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<SpawnResponse?> SpawnAsync(string playerId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/spawn")
        {
            Content = JsonContent.Create(new SpawnRequest(playerId)),
        };
        req.Headers.Add("X-Shard-Secret", _shardSecret);

        var response = await _http.SendAsync(req);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Shard returned {StatusCode} for SpawnAsync(playerId={PlayerId})",
                (int)response.StatusCode, playerId);
            response.EnsureSuccessStatusCode(); // throws HttpRequestException for callers to handle
        }
        return await response.Content.ReadFromJsonAsync<SpawnResponse>();
    }

    public async Task SendInputAsync(string playerId, InputEvent input)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/input")
            {
                Content = JsonContent.Create(new { playerId, input }),
            };
            req.Headers.Add("X-Shard-Secret", _shardSecret);
            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Shard returned {StatusCode} for SendInputAsync(playerId={PlayerId})",
                    (int)response.StatusCode, playerId);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendInputAsync failed — input lost for player {PlayerId}", playerId);
        }
    }
}
