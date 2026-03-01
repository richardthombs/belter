using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Tests.Physics;

public class AsteroidSchemaTests
{
    [Fact]
    public async Task EnsureCreated_AsteroidsTable_ContainsDynamicSimulationColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info('asteroids');";

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains(columns, name => string.Equals(name, "velocity_x", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "VelocityX", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(columns, name => string.Equals(name, "velocity_y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "VelocityY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(columns, name => string.Equals(name, "is_destroyed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "IsDestroyed", StringComparison.OrdinalIgnoreCase));
    }
}
