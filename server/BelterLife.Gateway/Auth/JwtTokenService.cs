using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BelterLife.Gateway.Auth;

/// <summary>
/// Generates and inspects JWT tokens for player authentication.
/// Token claims: sub (userId), name (username), jti (unique token id for revocation).
/// </summary>
public class JwtTokenService
{
    readonly JwtConfig config;

    public JwtTokenService(IOptions<JwtConfig> options)
    {
        config = options.Value;
    }

    /// <summary>Generates a signed JWT for the given Identity user.</summary>
    public string GenerateToken(IdentityUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config.Issuer,
            audience: config.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(config.ExpiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Extracts the JTI and expiry from a raw token string.
    /// Returns null if the token cannot be read (malformed — does not validate signature).
    /// </summary>
    public (string Jti, DateTimeOffset ExpiresAt)? ReadToken(string rawToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(rawToken)) return null;

        try
        {
            var jwt = handler.ReadJwtToken(rawToken);
            var jti = jwt.Id;
            var exp = jwt.ValidTo == DateTime.MinValue
                ? DateTimeOffset.UtcNow.AddHours(config.ExpiryHours)
                : new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);

            return string.IsNullOrEmpty(jti) ? null : (jti, exp);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
