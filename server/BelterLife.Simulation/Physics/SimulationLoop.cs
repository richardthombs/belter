using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BelterLife.Simulation.Tests")]

namespace BelterLife.Simulation.Physics;

/// <summary>IHostedService game tick — target 30 FPS (NFR2).</summary>
public class SimulationLoop : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IGatewayClient _gatewayClient;
    readonly int _tickRateMs;
    readonly ILogger<SimulationLoop> _logger;

    public SimulationLoop(
        IServiceScopeFactory scopeFactory,
        IGatewayClient gatewayClient,
        IConfiguration config,
        ILogger<SimulationLoop> logger)
    {
        _scopeFactory = scopeFactory;
        _gatewayClient = gatewayClient;
        _tickRateMs = config.GetValue<int>("TickRateMs", 33);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_tickRateMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await TickAsync(db, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tick threw an exception — continuing");
            }
        }
    }

    internal async Task TickAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var sectors = await db.Sectors.AsNoTracking().ToListAsync(cancellationToken);
        if (sectors.Count == 0) return;

        var sectorIds = sectors.Select(s => s.Id).ToList();
        var ships = await db.Ships
            .AsNoTracking()
            .Where(s => sectorIds.Contains(s.SectorId))
            .ToListAsync(cancellationToken);
        var asteroids = await db.Asteroids
            .AsNoTracking()
            .Where(a => sectorIds.Contains(a.SectorId))
            .ToListAsync(cancellationToken);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var sector in sectors)
        {
            var sectorShips = ships.Where(s => s.SectorId == sector.Id)
                .Select(s => new ShipSnapshot(s.Id, s.PlayerId, s.X, s.Y, s.VelocityX, s.VelocityY, s.Heading))
                .ToList();

            var sectorAsteroids = asteroids.Where(a => a.SectorId == sector.Id)
                .Select(a => new AsteroidSnapshot(a.Id, a.X, a.Y, a.Radius, a.VertexCount, a.RotationOffset))
                .ToList();

            var update = new WorldStateUpdate(sector.Id, timestamp, sectorShips, sectorAsteroids);

            try
            {
                await _gatewayClient.BroadcastAsync(update);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcast failed for SectorId={SectorId} — skipping", sector.Id);
            }
        }
    }
}
