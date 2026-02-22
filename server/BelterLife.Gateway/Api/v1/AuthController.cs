using System.IdentityModel.Tokens.Jwt;
using BelterLife.Gateway.Auth;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BelterLife.Gateway.Api.v1;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    readonly UserManager<IdentityUser> userManager;
    readonly JwtTokenService tokenService;
    readonly GatewayDbContext db;

    public AuthController(
        UserManager<IdentityUser> userManager,
        JwtTokenService tokenService,
        GatewayDbContext db)
    {
        this.userManager = userManager;
        this.tokenService = tokenService;
        this.db = db;
    }

    /// <summary>Register a new player account and return a JWT.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new IdentityUser { UserName = request.Username };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            return Problem(
                title: "Registration failed",
                detail: string.Join("; ", errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var token = tokenService.GenerateToken(user);
        return StatusCode(StatusCodes.Status201Created, new TokenResponse(token));
    }

    /// <summary>Authenticate an existing player and return a JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                title: "Authentication failed",
                detail: "Invalid username or password",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = tokenService.GenerateToken(user);
        return Ok(new TokenResponse(token));
    }

    /// <summary>Invalidate the current JWT by persisting its JTI to revoked_tokens.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var rawToken = HttpContext.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", string.Empty);

        if (rawToken is not null)
        {
            var tokenInfo = tokenService.ReadToken(rawToken);
            if (tokenInfo is not null)
            {
                db.RevokedTokens.Add(new RevokedToken
                {
                    Jti = tokenInfo.Value.Jti,
                    ExpiresAt = tokenInfo.Value.ExpiresAt,
                });
                await db.SaveChangesAsync();
            }
        }

        return NoContent();
    }
}

public record TokenResponse(string Token);
