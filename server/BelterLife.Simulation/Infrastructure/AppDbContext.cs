using BelterLife.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Infrastructure;

/// <summary>
/// EF Core DbContext for the simulation shard.
/// snake_case naming enforced via UseSnakeCaseNamingConvention() — do NOT override.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<Asteroid> Asteroids => Set<Asteroid>();
    public DbSet<Ship> Ships => Set<Ship>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<NpcStation> NpcStations => Set<NpcStation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Sector>(e =>
        {
            e.HasKey(s => s.Id);
        });

        modelBuilder.Entity<Asteroid>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasOne<Sector>().WithMany().HasForeignKey(a => a.SectorId);
            e.HasIndex(a => a.SectorId);
        });

        modelBuilder.Entity<Ship>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.PlayerId).HasMaxLength(450);
            e.HasIndex(s => s.PlayerId).IsUnique();
        });

        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasMaxLength(450);
        });

        modelBuilder.Entity<NpcStation>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Name).HasMaxLength(100);
            e.HasIndex(n => n.SectorId);
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connection string provided via IConfiguration / AddDbContext in Program.cs — do not override here.
    }
}
