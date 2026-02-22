using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Tests;

public class AppDbContextTests
{
    [Fact]
    public void AppDbContext_IsSubclassOfDbContext()
    {
        Assert.True(typeof(AppDbContext).IsSubclassOf(typeof(DbContext)));
    }
}
