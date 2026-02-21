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
        var response = await _shardClient.SpawnAsync(userId);
        return Ok(response);
    }
}
