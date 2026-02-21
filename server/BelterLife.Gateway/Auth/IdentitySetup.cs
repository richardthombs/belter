using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BelterLife.Gateway.Auth;

/// <summary>
/// Registers ASP.NET Core Identity + JWT Bearer auth for the Gateway.
/// Call builder.Services.AddBelterIdentity(builder.Configuration) in Program.cs.
/// </summary>
public static class IdentitySetup
{
    public static IServiceCollection AddBelterIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── DbContext ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required");

        services.AddDbContext<GatewayDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        // ── Identity ───────────────────────────────────────────────────────────
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<GatewayDbContext>()
            .AddDefaultTokenProviders();

        // ── JWT config ─────────────────────────────────────────────────────────
        services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
        services.AddScoped<JwtTokenService>();

        // ── JWT Bearer authentication ──────────────────────────────────────────
        var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfig>()
            ?? throw new InvalidOperationException("Jwt configuration section is required");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtConfig.Issuer,
                    ValidAudience = jwtConfig.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtConfig.Key)),
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    // SignalR WebSocket upgrade passes JWT as ?access_token= query param (browser limitation)
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    // Token revocation check — one DB query per authenticated request
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(
                            System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

                        if (jti is not null)
                        {
                            var db = context.HttpContext.RequestServices
                                           .GetRequiredService<GatewayDbContext>();
                            if (await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
                                context.Fail("Token has been revoked");
                        }
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }
}
