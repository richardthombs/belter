using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BelterLife.Gateway.Api.v1;

[ApiController]
[Route("api/v1/players")]
[Authorize]
public class PlayersController : ControllerBase
{
    readonly IShardClient _shardClient;

    public PlayersController(IShardClient shardClient)
    {
        _shardClient = shardClient;
    }

    [HttpPost("me/spawn")]
    public async Task<IActionResult> Spawn()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        try
        {
            var response = await _shardClient.SpawnAsync(userId);
            if (response is null)
                return StatusCode(502, "Shard returned an empty response.");
            return Ok(response);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, "Shard unavailable.");
        }
    }
}
