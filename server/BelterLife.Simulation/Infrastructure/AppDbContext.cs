using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Infrastructure;

/// <summary>
/// EF Core DbContext for the simulation shard.
/// snake_case naming enforced via UseSnakeCaseNamingConvention() — do NOT override.
/// No DbSet properties yet — tables added JIT in stories that introduce them.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
