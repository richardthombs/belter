using BelterLife.Gateway.Hubs;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BelterLife.Gateway.Tests.Hubs;

public class BroadcastControllerTests
{
    private static IConfiguration BuildConfig(string secret = "test-secret") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SHARD_SECRET"] = secret })
            .Build();

    private static WorldStateUpdate SampleUpdate(int sectorId = 1) =>
        new WorldStateUpdate(sectorId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new List<ShipSnapshot> { new ShipSnapshot(1, "player-1", 0f, 0f, 0f, 0f, 0f) },
            new List<AsteroidSnapshot> { new AsteroidSnapshot(1, 100f, 100f, 20f, 8, 0f) });

    private static BroadcastController CreateController(
        IHubContext<GameHub> hubContext,
        IConfiguration config,
        string? secretHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        if (secretHeader is not null)
            httpContext.Request.Headers["X-Shard-Secret"] = secretHeader;

        var controller = new BroadcastController(hubContext, config);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Broadcast_WithValidSecret_CallsHubContext()
    {
        // Arrange
        var update = SampleUpdate(sectorId: 42);

        var mockGroupProxy = new Mock<IClientProxy>();
        mockGroupProxy
            .Setup(p => p.SendCoreAsync(
                "WorldStateUpdate",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.Group("sector-42")).Returns(mockGroupProxy.Object);

        var mockHubContext = new Mock<IHubContext<GameHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var controller = CreateController(mockHubContext.Object, BuildConfig(), secretHeader: "test-secret");

        // Act
        var result = await controller.Broadcast(update);

        // Assert
        Assert.IsType<OkResult>(result);
        mockClients.Verify(c => c.Group("sector-42"), Times.Once());
        mockGroupProxy.Verify(
            p => p.SendCoreAsync("WorldStateUpdate", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Broadcast_MissingSecret_Returns403()
    {
        // Arrange
        var update = SampleUpdate();
        var mockHubContext = new Mock<IHubContext<GameHub>>();
        var controller = CreateController(mockHubContext.Object, BuildConfig(), secretHeader: null);

        // Act
        var result = await controller.Broadcast(update);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task Broadcast_WrongSecret_Returns403()
    {
        // Arrange
        var update = SampleUpdate();
        var mockHubContext = new Mock<IHubContext<GameHub>>();
        var controller = CreateController(mockHubContext.Object, BuildConfig("correct-secret"), secretHeader: "wrong-secret");

        // Act
        var result = await controller.Broadcast(update);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }
}
