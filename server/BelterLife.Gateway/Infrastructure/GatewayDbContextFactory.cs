using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BelterLife.Gateway.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migration tooling (dotnet ef migrations add).
/// Not used at runtime — connection string is provided via IConfiguration in production.
/// </summary>
public class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=belterlife;Username=belter;Password=postgres";

        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new GatewayDbContext(options);
    }
}
