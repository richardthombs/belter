using System.Net.Http.Json;
using BelterLife.Shared.Contracts.Api;

namespace BelterLife.Gateway.Infrastructure;

/// <summary>Typed HttpClient for communicating with the simulation shard's internal API.</summary>
public class ShardClient : IShardClient
{
    readonly HttpClient _http;
    readonly string _shardSecret;

    public ShardClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _shardSecret = config["SHARD_SECRET"] ?? string.Empty;
    }

    public async Task<SpawnResponse?> SpawnAsync(string playerId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/spawn")
        {
            Content = JsonContent.Create(new SpawnRequest(playerId)),
        };
        req.Headers.Add("X-Shard-Secret", _shardSecret);

        var response = await _http.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpawnResponse>();
    }
}
