using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BelterLife.Gateway.Hubs;
using BelterLife.Gateway.Infrastructure;
using BelterLife.Shared.Contracts.Api;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace BelterLife.Gateway.Tests.Hubs;

public class GameHubTests
{
    private static GameHub CreateHub(
        IShardClient shardClient,
        out Mock<HubCallerContext> mockContext,
        out Mock<IGroupManager> mockGroups,
        string userId = "user-1",
        int sectorId = 1)
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
        }));

        mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("conn-1");
        mockContext.Setup(c => c.User).Returns(claims);

        mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = new GameHub(shardClient);
        hub.Context = mockContext.Object;
        hub.Groups = mockGroups.Object;
        return hub;
    }

    [Fact]
    public async Task OnConnectedAsync_AddsConnectionToSectorGroup()
    {
        // Arrange
        var mockShardClient = new Mock<IShardClient>();
        mockShardClient
            .Setup(s => s.SpawnAsync("user-1"))
            .ReturnsAsync(new SpawnResponse(SectorId: 1, ShipId: 10, SpawnX: 0L, SpawnY: 0L));

        var hub = CreateHub(mockShardClient.Object, out var mockContext, out var mockGroups);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockGroups.Verify(
            g => g.AddToGroupAsync("conn-1", "sector-1", It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task OnConnectedAsync_AbortsWhenShardUnavailable()
    {
        // Arrange
        var mockShardClient = new Mock<IShardClient>();
        mockShardClient
            .Setup(s => s.SpawnAsync("user-1"))
            .ReturnsAsync((SpawnResponse?)null);

        var hub = CreateHub(mockShardClient.Object, out var mockContext, out var mockGroups);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockContext.Verify(c => c.Abort(), Times.Once());
        mockGroups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task OnConnectedAsync_AbortsWhenUserIdMissing()
    {
        // Arrange — principal with no Sub claim
        var claims = new ClaimsPrincipal(new ClaimsIdentity());
        var mockShardClient = new Mock<IShardClient>();

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns("conn-1");
        mockContext.Setup(c => c.User).Returns(claims);
        var mockGroups = new Mock<IGroupManager>();

        var hub = new GameHub(mockShardClient.Object);
        hub.Context = mockContext.Object;
        hub.Groups = mockGroups.Object;

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockContext.Verify(c => c.Abort(), Times.Once());
        mockShardClient.Verify(s => s.SpawnAsync(It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task OnDisconnectedAsync_RemovesConnectionFromSectorGroup()
    {
        // Arrange
        var mockShardClient = new Mock<IShardClient>();
        mockShardClient
            .Setup(s => s.SpawnAsync("user-1"))
            .ReturnsAsync(new SpawnResponse(SectorId: 5, ShipId: 10, SpawnX: 0L, SpawnY: 0L));

        var hub = CreateHub(mockShardClient.Object, out var mockContext, out var mockGroups);
        mockGroups
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Establish connection so _sectorGroup is set
        await hub.OnConnectedAsync();

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        mockGroups.Verify(
            g => g.RemoveFromGroupAsync("conn-1", "sector-5", It.IsAny<CancellationToken>()),
            Times.Once());
    }
}
