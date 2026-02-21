using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using BelterLife.Gateway.Api.v1;
using BelterLife.Gateway.Infrastructure;
using BelterLife.Shared.Contracts.Api;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BelterLife.Gateway.Tests.Api;

public class PlayersControllerTests
{
    static PlayersController CreateController(IShardClient shardClient, string userId = "user-123")
    {
        var controller = new PlayersController(shardClient);
        var claims = new[] { new Claim("sub", userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
        return controller;
    }

    /// <summary>AC 1 — authenticated user calls spawn, ShardClient is called, Ok is returned.</summary>
    [Fact]
    public async Task Spawn_AuthenticatedUser_CallsShardAndReturnsOk()
    {
        var expected = new SpawnResponse(1, 1, 0f, 0f);
        var mock = new Mock<IShardClient>();
        mock.Setup(s => s.SpawnAsync("user-abc")).ReturnsAsync(expected);
        var controller = CreateController(mock.Object, "user-abc");

        var result = await controller.Spawn();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpawnResponse>(ok.Value);
        Assert.Equal(1, response.SectorId);
        Assert.Equal(1, response.ShipId);
        mock.Verify(s => s.SpawnAsync("user-abc"), Times.Once);
    }

    /// <summary>AC 1 — unauthenticated requests are blocked by [Authorize] middleware (401).</summary>
    [Fact]
    public async Task Spawn_UnauthenticatedUser_Returns401()
    {
        using var factory = new GatewayWebApplicationFactory();
        var client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        var res = await client.PostAsync("/api/v1/players/me/spawn", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
