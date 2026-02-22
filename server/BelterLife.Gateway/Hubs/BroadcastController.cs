using BelterLife.Gateway.Hubs;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BelterLife.Gateway.Hubs;

[ApiController]
[Route("api/internal")]
public class BroadcastController : ControllerBase
{
    readonly IHubContext<GameHub> _hubContext;
    readonly string _shardSecret;

    public BroadcastController(IHubContext<GameHub> hubContext, IConfiguration config)
    {
        _hubContext = hubContext;
        _shardSecret = config["SHARD_SECRET"] ?? string.Empty;
        if (string.IsNullOrEmpty(_shardSecret))
            throw new InvalidOperationException("SHARD_SECRET is not configured — broadcast endpoint cannot enforce shard authentication.");
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] WorldStateUpdate update)
    {
        if (!Request.Headers.TryGetValue("X-Shard-Secret", out var header) || header != _shardSecret)
            return StatusCode(403);

        await _hubContext.Clients
            .Group($"sector-{update.SectorId}")
            .SendAsync("WorldStateUpdate", update);

        return Ok();
    }
}
