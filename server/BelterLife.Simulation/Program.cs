using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddHostedService<SimulationLoop>();

var host = builder.Build();
host.Run();

