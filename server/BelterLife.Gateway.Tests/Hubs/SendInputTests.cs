using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BelterLife.Gateway.Hubs;
using BelterLife.Gateway.Infrastructure;
using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Contracts.Hubs;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace BelterLife.Gateway.Tests.Hubs;

public class SendInputTests
{
	private static GameHub CreateHub(
		IShardClient shardClient,
		out Mock<HubCallerContext> mockContext,
		string? userId = "user-1")
	{
		var identity = userId is null
			? new ClaimsIdentity()
			: new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, userId) });
		var claims = new ClaimsPrincipal(identity);

		mockContext = new Mock<HubCallerContext>();
		mockContext.Setup(c => c.ConnectionId).Returns("conn-1");
		mockContext.Setup(c => c.User).Returns(claims);

		// Groups mock is needed because GameHub.OnConnectedAsync accesses it,
		// but SendInput tests don't call OnConnectedAsync — provide a no-op.
		var mockGroups = new Mock<IGroupManager>();
		mockGroups
			.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var hub = new GameHub(shardClient);
		hub.Context = mockContext.Object;
		hub.Groups  = mockGroups.Object;
		return hub;
	}

	[Fact]
	public async Task SendInput_WhenUserAuthenticated_ForwardsToShardClient()
	{
		// Arrange
		var mockShard = new Mock<IShardClient>();
		mockShard
			.Setup(s => s.SpawnAsync(It.IsAny<string>()))
			.ReturnsAsync(new SpawnResponse(SectorId: 1, ShipId: 10, SpawnX: 0f, SpawnY: 0f));
		mockShard
			.Setup(s => s.SendInputAsync(It.IsAny<string>(), It.IsAny<InputEvent>()))
			.Returns(Task.CompletedTask);

		var hub   = CreateHub(mockShard.Object, out _);
		var input = new InputEvent(Thrust: 1f, Torque: 0f, Brake: false);

		// Act
		await hub.SendInput(input);

		// Assert
		mockShard.Verify(
			s => s.SendInputAsync("user-1", It.Is<InputEvent>(e =>
				e.Thrust == 1f && e.Torque == 0f && !e.Brake)),
			Times.Once());
	}

	[Fact]
	public async Task SendInput_WhenUserIdMissing_DoesNotCallShard()
	{
		// Arrange — hub context has no Sub claim
		var mockShard = new Mock<IShardClient>();
		var hub       = CreateHub(mockShard.Object, out _, userId: null);
		var input     = new InputEvent(Thrust: 1f, Torque: 0f, Brake: false);

		// Act
		await hub.SendInput(input);

		// Assert — shard never called
		mockShard.Verify(s => s.SendInputAsync(It.IsAny<string>(), It.IsAny<InputEvent>()), Times.Never());
	}
}
