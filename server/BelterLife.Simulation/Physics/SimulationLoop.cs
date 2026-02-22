namespace BelterLife.Simulation.Physics;

/// <summary>IHostedService game tick — target 30-60 FPS (NFR3).</summary>
public class SimulationLoop : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
