using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Simulation.Api;
using BelterLife.Simulation.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BelterLife.Simulation.Tests.Api;

public class InputControllerTests
{
	private const string ValidSecret = "test-secret";

	private static InputController CreateController(IInputBuffer buffer, string? configuredSecret = ValidSecret)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["SHARD_SECRET"] = configuredSecret,
			})
			.Build();

		return new InputController(buffer, config);
	}

	[Fact]
	public void Constructor_ThrowsWhenSecretNotConfigured()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();

		Assert.Throws<InvalidOperationException>(() =>
			new InputController(new Mock<IInputBuffer>().Object, config));
	}

	[Fact]
	public void Post_ValidSecret_StoresInputAndReturnsNoContent()
	{
		var mockBuffer = new Mock<IInputBuffer>();
		var controller = CreateController(mockBuffer.Object);
		var request    = new InputRequest("player-1", new InputEvent(Thrust: 1f, Torque: 0f, Brake: false));

		var result = controller.Post(request, ValidSecret);

		Assert.IsType<NoContentResult>(result);
		mockBuffer.Verify(
			b => b.Set("player-1", It.Is<InputEvent>(e => e.Thrust == 1f && e.Torque == 0f && !e.Brake)),
			Times.Once());
	}

	[Fact]
	public void Post_InvalidSecret_ReturnsForbidAndDoesNotCallBuffer()
	{
		var mockBuffer = new Mock<IInputBuffer>();
		var controller = CreateController(mockBuffer.Object);
		var request    = new InputRequest("player-1", new InputEvent(Thrust: 1f, Torque: 0f, Brake: false));

		var result = controller.Post(request, "wrong-secret");

		Assert.IsType<ForbidResult>(result);
		mockBuffer.Verify(b => b.Set(It.IsAny<string>(), It.IsAny<InputEvent>()), Times.Never());
	}

	[Fact]
	public void Post_MissingSecret_ReturnsForbidAndDoesNotCallBuffer()
	{
		var mockBuffer = new Mock<IInputBuffer>();
		var controller = CreateController(mockBuffer.Object);
		var request    = new InputRequest("player-1", new InputEvent(Thrust: 0f, Torque: 0f, Brake: false));

		var result = controller.Post(request, secret: null);

		Assert.IsType<ForbidResult>(result);
		mockBuffer.Verify(b => b.Set(It.IsAny<string>(), It.IsAny<InputEvent>()), Times.Never());
	}
}
