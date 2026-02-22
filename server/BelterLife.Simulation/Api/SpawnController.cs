using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Entities;
using BelterLife.Simulation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BelterLife.Simulation.Api;

[ApiController]
[Route("api/internal")]
public class SpawnController : ControllerBase
{
    readonly AppDbContext _db;
    readonly SectorGenerator _sectorGenerator;
    readonly IConfiguration _config;

    public SpawnController(AppDbContext db, SectorGenerator sectorGenerator, IConfiguration config)
    {
        _db = db;
        _sectorGenerator = sectorGenerator;
        _config = config;
    }

    [HttpPost("spawn")]
    public async Task<IActionResult> Spawn([FromBody] SpawnRequest request)
    {
        var secret = _config["SHARD_SECRET"];
        if (!Request.Headers.TryGetValue("X-Shard-Secret", out var header) || header != secret)
            return StatusCode(403);

        var existing = await _db.Players.FirstOrDefaultAsync(p => p.Id == request.PlayerId);
        if (existing != null)
        {
            return Ok(new SpawnResponse(existing.SectorId, existing.ShipId, 0f, 0f));
        }

        var (sector, asteroids, stations) = _sectorGenerator.Generate(_sectorGenerator.NewSeed());

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Save 1: persist Sector to obtain its auto-generated Id
            await _db.Sectors.AddAsync(sector);
            await _db.SaveChangesAsync();

            // Save 2: persist Asteroids + NpcStations + Ship (all need sector.Id; Ship.Id needed for Player)
            foreach (var a in asteroids) a.SectorId = sector.Id;
            foreach (var s in stations) s.SectorId = sector.Id;
            await _db.Asteroids.AddRangeAsync(asteroids);
            await _db.NpcStations.AddRangeAsync(stations);

            var ship = new Ship
            {
                PlayerId = request.PlayerId,
                SectorId = sector.Id,
                X = 0,
                Y = 0,
                VelocityX = 0,
                VelocityY = 0,
                Heading = 0,
            };
            await _db.Ships.AddAsync(ship);
            await _db.SaveChangesAsync(); // resolves ship.Id

            // Save 3: persist Player (needs both sector.Id and ship.Id) + commit
            var player = new Player
            {
                Id = request.PlayerId,
                SectorId = sector.Id,
                ShipId = ship.Id,
                LastSeenAt = DateTimeOffset.UtcNow,
            };
            await _db.Players.AddAsync(player);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return StatusCode(201, new SpawnResponse(sector.Id, ship.Id, 0f, 0f));
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
