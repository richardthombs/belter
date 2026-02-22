using BelterLife.Simulation.Entities;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
}

builder.Services.AddSingleton<SectorGenerator>();
builder.Services.AddHostedService<SimulationLoop>();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

app.MapControllers();
app.Run();

