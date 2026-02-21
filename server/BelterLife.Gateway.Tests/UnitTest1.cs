using BelterLife.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BelterLife.Gateway.Tests;

public class GameHubTests
{
    [Fact]
    public void GameHub_IsSubclassOfHub()
    {
        Assert.True(typeof(GameHub).IsSubclassOf(typeof(Hub)));
    }
}
