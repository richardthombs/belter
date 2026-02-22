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

		if (string.IsNullOrWhiteSpace(jwtConfig.Key))
			throw new InvalidOperationException(
				"Jwt:Key is required. Set the Jwt__Key environment variable (minimum 32 characters for HMACSHA256).");
		if (Encoding.UTF8.GetByteCount(jwtConfig.Key) < 32)
			throw new InvalidOperationException(
				$"Jwt:Key must be at least 32 bytes for HMACSHA256 (current: {Encoding.UTF8.GetByteCount(jwtConfig.Key)} bytes).");

		services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
			{
				options.MapInboundClaims = false;
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
					// Return RFC 9457 Problem Details on 401 challenge (no/invalid token)
					OnChallenge = async context =>
					{
						context.HandleResponse();
						context.Response.StatusCode = 401;
						context.Response.ContentType = "application/problem+json";
						var body = System.Text.Json.JsonSerializer.Serialize(new
						{
							type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
							title = "Unauthorized",
							status = 401,
							detail = string.IsNullOrEmpty(context.ErrorDescription)
								? "A valid bearer token is required."
								: context.ErrorDescription,
						});
						await context.Response.WriteAsync(body);
					},
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
