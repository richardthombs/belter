using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Simulation.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BelterLife.Simulation.Api;

[ApiController]
[Route("api/internal/input")]
public class InputController : ControllerBase
{
    readonly IInputBuffer _buffer;
    readonly string _secret;

    public InputController(IInputBuffer buffer, IConfiguration config)
    {
        _buffer = buffer;
        _secret = config["SHARD_SECRET"]
            ?? throw new InvalidOperationException("SHARD_SECRET is not configured");
    }

    [HttpPost]
    public IActionResult Post(
        [FromBody] InputRequest request,
        [FromHeader(Name = "X-Shard-Secret")] string? secret)
    {
        if (secret != _secret) return Forbid();
        _buffer.Set(request.PlayerId, request.Input);
        return NoContent();
    }
}

public record InputRequest(string PlayerId, InputEvent Input);
