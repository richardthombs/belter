using BelterLife.Gateway.Auth;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BelterLife.Gateway.Tests;

public class GameHubTests
{
    [Fact]
    public void GameHub_IsSubclassOfHub()
    {
        Assert.True(typeof(BelterLife.Gateway.Hubs.GameHub)
            .IsSubclassOf(typeof(Microsoft.AspNetCore.SignalR.Hub)));
    }
}

// ── Shared test helpers ────────────────────────────────────────────────────────

static class TestFactory
{
    public static GatewayDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new GatewayDbContext(options);
    }

    public static JwtTokenService CreateTokenService(string key = "test-key-that-is-32-chars-long!!")
    {
        var config = new JwtConfig
        {
            Key = key,
            Issuer = "test",
            Audience = "test",
            ExpiryHours = 1,
        };
        return new JwtTokenService(Options.Create(config));
    }

    public static UserManager<IdentityUser> CreateUserManager(GatewayDbContext db)
    {
        var store = new UserStore<IdentityUser>(db);
        var normalizer = new UpperInvariantLookupNormalizer();
        // Match production IdentitySetup.cs password options
        var identityOptions = Options.Create(new IdentityOptions
        {
            Password = new PasswordOptions
            {
                RequireDigit = false,
                RequireUppercase = false,
                RequireNonAlphanumeric = false,
                RequiredLength = 6,
            },
        });
        return new UserManager<IdentityUser>(
            store,
            identityOptions,
            new PasswordHasher<IdentityUser>(),
            new IUserValidator<IdentityUser>[] { new UserValidator<IdentityUser>() },
            new IPasswordValidator<IdentityUser>[] { new PasswordValidator<IdentityUser>() },
            normalizer,
            new IdentityErrorDescriber(),
            null!,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<IdentityUser>>());
    }
}

