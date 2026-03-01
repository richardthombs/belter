using BelterLife.Shared.Contracts.Hubs;
using BelterLife.Simulation.Entities;
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
    readonly IInputBuffer _inputBuffer;
    readonly PhysicsEngine _physicsEngine;
    readonly AsteroidManager _asteroidManager;
    readonly int _tickRateMs;
    readonly ILogger<SimulationLoop> _logger;
    int _tickCount = 0;
    // At 33 ms/tick, 20 ticks ≈ 660 ms between reconciliation snapshots sent to clients.
    // Increase to reduce bandwidth; decrease to improve client correction latency.
    const int ReconcileIntervalTicks = 20;

    public SimulationLoop(
        IServiceScopeFactory scopeFactory,
        IGatewayClient gatewayClient,
        IInputBuffer inputBuffer,
        PhysicsEngine physicsEngine,
        AsteroidManager asteroidManager,
        IConfiguration config,
        ILogger<SimulationLoop> logger)
    {
        _scopeFactory = scopeFactory;
        _gatewayClient = gatewayClient;
        _inputBuffer = inputBuffer;
        _physicsEngine = physicsEngine;
        _asteroidManager = asteroidManager;
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
        bool includeInput = (_tickCount++ % ReconcileIntervalTicks) == 0;
        var sectors = await db.Sectors.AsNoTracking().ToListAsync(cancellationToken);
        if (sectors.Count == 0) return;

        var sectorIds = sectors.Select(s => s.Id).ToList();
        // Ships are tracked (no AsNoTracking) so EF can persist physics mutations via SaveChangesAsync.
        var ships = await db.Ships
            .Where(s => sectorIds.Contains(s.SectorId))
            .ToListAsync(cancellationToken);
        var playerIds = ships.Select(s => s.PlayerId).Distinct().ToList();
        var playerCreditsById = await db.Players
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Credits, cancellationToken);

        // Apply physics to every ship using last-known input from each player.
        float dt = _tickRateMs / 1000f;
        var inputs = _inputBuffer.GetAll();
        foreach (var ship in ships)
        {
            inputs.TryGetValue(ship.PlayerId, out var input);
            _physicsEngine.ApplyPhysics(ship, input, dt);
        }

        var asteroids = await _asteroidManager.UpdateAsync(db, sectorIds, dt, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var sector in sectors)
        {
            var sectorShips = ships.Where(s => s.SectorId == sector.Id)
                .Select(s =>
                {
                    float? thrust = null;
                    float? torque = null;
                    if (includeInput && inputs.TryGetValue(s.PlayerId, out var inp))
                    {
                        thrust = inp.Thrust;
                        torque = inp.Torque;
                    }
                    var credits = playerCreditsById.TryGetValue(s.PlayerId, out var value) ? value : 0;
                    return new ShipSnapshot(
                        s.Id,
                        s.PlayerId,
                        s.X,
                        s.Y,
                        s.VelocityX,
                        s.VelocityY,
                        s.Heading,
                        thrust,
                        torque,
                        s.SectorId,
                        credits,
                        0,
                        100);
                })
                .ToList();

            var sectorAsteroids = asteroids
                .Where(a => a.SectorId == sector.Id && !a.IsDestroyed)
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
