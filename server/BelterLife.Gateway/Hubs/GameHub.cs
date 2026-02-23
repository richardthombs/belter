using System.IdentityModel.Tokens.Jwt;
using BelterLife.Gateway.Infrastructure;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BelterLife.Gateway.Hubs;

/// <summary>
/// SignalR hub — player input (Client→Server) and world state (Server→Client).
/// MessagePack protocol registered in Program.cs via AddMessagePackProtocol().
/// Server→Client messages: PascalCase (e.g. WorldStateUpdate, EntityHandoff).
/// Client→Server methods: PascalCase (e.g. SendInput, InitiateJump).
/// JWT passed as query param: ?access_token=... on WebSocket upgrade.
/// </summary>
[Authorize]
public class GameHub : Hub
{
    readonly IShardClient _shardClient;
    private string? _sectorGroup;

    public GameHub(IShardClient shardClient)
    {
        _shardClient = shardClient;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        var response = await _shardClient.SpawnAsync(userId);
        if (response is null)
        {
            Context.Abort();
            return;
        }

        _sectorGroup = $"sector-{response.SectorId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, _sectorGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_sectorGroup is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, _sectorGroup);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Receives player input and forwards to the shard.
    /// Client sends PascalCase fields: { Thrust, Torque, Brake } via ContractlessStandardResolver.
    /// </summary>
    public async Task SendInput(InputEvent input)
    {
        var userId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null) return;
        await _shardClient.SendInputAsync(userId, input);
    }
}
