using BelterLife.Gateway.Api.v1;
using BelterLife.Gateway.Auth;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BelterLife.Gateway.Tests;

public class AuthControllerTests
{
    static (AuthController controller, GatewayDbContext db, UserManager<IdentityUser> um) CreateSut(string dbName)
    {
        var db = TestFactory.CreateInMemoryDb(dbName);
        var um = TestFactory.CreateUserManager(db);
        var tokenService = TestFactory.CreateTokenService();
        var controller = new AuthController(um, tokenService, db);
        return (controller, db, um);
    }

    // ── Register ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidCredentials_Returns201WithToken()
    {
        var (sut, _, _) = CreateSut(nameof(Register_WithValidCredentials_Returns201WithToken));

        var result = await sut.Register(new RegisterRequest("newplayer", "password123"));

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var response = Assert.IsType<TokenResponse>(created.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns400ProblemDetails()
    {
        var (sut, _, um) = CreateSut(nameof(Register_WithDuplicateUsername_Returns400ProblemDetails));
        // Pre-seed an existing user
        await um.CreateAsync(new IdentityUser { UserName = "taken" }, "existing123");

        var result = await sut.Register(new RegisterRequest("taken", "newpassword"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    // ── Login ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var (sut, _, um) = CreateSut(nameof(Login_WithValidCredentials_Returns200WithToken));
        await um.CreateAsync(new IdentityUser { UserName = "player1" }, "securepass");

        var result = await sut.Login(new LoginRequest("player1", "securepass"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TokenResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401ProblemDetails()
    {
        var (sut, _, um) = CreateSut(nameof(Login_WithInvalidPassword_Returns401ProblemDetails));
        await um.CreateAsync(new IdentityUser { UserName = "player2" }, "correctpass");

        var result = await sut.Login(new LoginRequest("player2", "wrongpass"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401ProblemDetails()
    {
        var (sut, _, _) = CreateSut(nameof(Login_WithUnknownUser_Returns401ProblemDetails));

        var result = await sut.Login(new LoginRequest("nobody", "whatever"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    // ── Logout / Revocation ────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithValidToken_Returns204AndPersistsRevocation()
    {
        var (sut, db, um) = CreateSut(nameof(Logout_WithValidToken_Returns204AndPersistsRevocation));
        var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "logoutplayer" };
        var tokenService = TestFactory.CreateTokenService();
        var rawToken = tokenService.GenerateToken(user);

        // Simulate authenticated request with Bearer token in header
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        sut.HttpContext.Request.Headers.Authorization = $"Bearer {rawToken}";

        var result = await sut.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Single(db.RevokedTokens.ToList());
    }
}
