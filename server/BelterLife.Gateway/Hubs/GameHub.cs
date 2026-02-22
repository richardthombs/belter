using Microsoft.AspNetCore.SignalR;

namespace BelterLife.Gateway.Hubs;

/// <summary>
/// SignalR hub — player input (Client→Server) and world state (Server→Client).
/// MessagePack protocol registered in Program.cs via AddMessagePackProtocol().
/// Server→Client messages: PascalCase (e.g. WorldStateUpdate, EntityHandoff).
/// Client→Server methods: PascalCase (e.g. SendInput, InitiateJump).
/// JWT passed as query param: ?access_token=... on WebSocket upgrade.
/// </summary>
public class GameHub : Hub
{
}
