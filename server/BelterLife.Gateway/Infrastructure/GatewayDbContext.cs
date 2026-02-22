using BelterLife.Gateway.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Gateway.Infrastructure;

/// <summary>
/// Gateway's EF Core context — manages ASP.NET Core Identity tables and token revocation.
/// UseSnakeCaseNamingConvention() is applied via DI options (Program.cs / IdentitySetup.cs).
/// Identity table names will be snake_case: asp_net_users, asp_net_roles, etc.
/// </summary>
public class GatewayDbContext : IdentityDbContext<IdentityUser>
{
    public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options)
    {
    }

    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // must be first — wires Identity table mappings

        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.HasKey(t => t.Jti);
            entity.Property(t => t.Jti).HasMaxLength(256);
        });
    }
}
