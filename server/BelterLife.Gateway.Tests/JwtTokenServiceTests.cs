using BelterLife.Gateway.Auth;
using Microsoft.Extensions.Options;

namespace BelterLife.Gateway.Tests;

public class JwtTokenServiceTests
{
    readonly JwtTokenService sut = TestFactory.CreateTokenService();

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var user = new Microsoft.AspNetCore.Identity.IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testplayer",
        };

        var token = sut.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ThenReadToken_ReturnsSameJti()
    {
        var user = new Microsoft.AspNetCore.Identity.IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testplayer",
        };

        var token = sut.GenerateToken(user);
        var info = sut.ReadToken(token);

        Assert.NotNull(info);
        Assert.False(string.IsNullOrEmpty(info!.Value.Jti));
    }

    [Fact]
    public void ReadToken_WithGibberish_ReturnsNull()
    {
        var result = sut.ReadToken("not.a.jwt");

        Assert.Null(result);
    }

    [Fact]
    public void GenerateToken_ExpiresAt_IsInFuture()
    {
        var user = new Microsoft.AspNetCore.Identity.IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "futureplayer",
        };

        var token = sut.GenerateToken(user);
        var info = sut.ReadToken(token);

        Assert.NotNull(info);
        Assert.True(info!.Value.ExpiresAt > DateTimeOffset.UtcNow);
    }
}
