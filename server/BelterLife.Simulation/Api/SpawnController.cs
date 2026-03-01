using BelterLife.Shared.Contracts.Api;
using BelterLife.Shared.Entities;
using BelterLife.Simulation.Entities;
using BelterLife.Simulation.Infrastructure;
using BelterLife.Simulation.Physics;
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

		if (string.IsNullOrWhiteSpace(request.PlayerId))
			return BadRequest();

		var existing = await _db.Players.FirstOrDefaultAsync(p => p.Id == request.PlayerId);
		if (existing != null)
		{
			var ship = await _db.Ships.FirstOrDefaultAsync(s => s.Id == existing.ShipId);
			if (ship is null)
			{
				// Defensive: ship row must always exist for a persisted player — fail fast
				return StatusCode(500);
			}

			var asteroids = await _db.Asteroids
				.Where(a => a.SectorId == existing.SectorId)
				.AsNoTracking()
				.ToListAsync();

			const float SafeMargin = 10_000f;
			const float StepSize = 80_000f;
			const int MaxSteps = 10;
			int maxSearchSteps = (int)Math.Ceiling(RegionBounds.HalfSector / (double)StepSize);

			bool Overlaps(long x, long y) =>
				asteroids.Any(a =>
				{
					double dx = (double)a.X - x;
					double dy = (double)a.Y - y;
					double minDistance = a.Radius + SafeMargin;
					return dx * dx + dy * dy < minDistance * minDistance;
				});

			bool TryFindSafePosition(long originX, long originY, int searchSteps, out long safeX, out long safeY)
			{
				for (int step = 1; step <= searchSteps; step++)
				{
					long d = (long)(step * StepSize);
					var candidates = new (long dx, long dy)[]
						{ (d, 0L), (-d, 0L), (0L, d), (0L, -d), (d, d), (-d, d), (d, -d), (-d, -d) };
					foreach (var (dx, dy) in candidates)
					{
						long candidateX = originX + dx;
						long candidateY = originY + dy;
						if (!Overlaps(candidateX, candidateY))
						{
							safeX = candidateX;
							safeY = candidateY;
							return true;
						}
					}
				}

				safeX = originX;
				safeY = originY;
				return false;
			}

			bool repositioned = false;
			if (Overlaps(ship.X, ship.Y))
			{
				long originX = ship.X;
				long originY = ship.Y;
				bool found = TryFindSafePosition(originX, originY, MaxSteps, out var safeX, out var safeY);
				if (!found)
					found = TryFindSafePosition(originX, originY, maxSearchSteps, out safeX, out safeY);
				if (!found && !Overlaps(0L, 0L))
				{
					safeX = 0L;
					safeY = 0L;
					found = true;
				}
				if (!found)
					return Conflict();

				ship.X = safeX;
				ship.Y = safeY;
				ship.VelocityX = 0f;
				ship.VelocityY = 0f;
				ship.Heading = 0f;
				ship.AngularVelocity = 0f;
				repositioned = true;
			}

			existing.LastSeenAt = DateTimeOffset.UtcNow;
			await _db.SaveChangesAsync();

			return Ok(new SpawnResponse(existing.SectorId, existing.ShipId, ship.X, ship.Y, repositioned));
		}

		var (sector, asteroidList, stations) = _sectorGenerator.Generate(_sectorGenerator.NewSeed(), gridX: 0, gridY: 0);

		await using var tx = await _db.Database.BeginTransactionAsync();
		try
		{
			// Save 1: persist Sector to obtain its auto-generated Id
			await _db.Sectors.AddAsync(sector);
			await _db.SaveChangesAsync();

			// Save 2: persist Asteroids + NpcStations + Ship (all need sector.Id; Ship.Id needed for Player)
			foreach (var a in asteroidList) a.SectorId = sector.Id;
			foreach (var s in stations) s.SectorId = sector.Id;
			await _db.Asteroids.AddRangeAsync(asteroidList);
			await _db.NpcStations.AddRangeAsync(stations);

			var ship = new Ship
			{
				PlayerId = request.PlayerId,
				SectorId = sector.Id,
				X = 0L,
				Y = 0L,
				VelocityX = 0f,
				VelocityY = 0f,
				Heading = 0f,
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
				Credits = 500,
			};
			await _db.Players.AddAsync(player);
			await _db.SaveChangesAsync();
			await tx.CommitAsync();

			return StatusCode(201, new SpawnResponse(sector.Id, ship.Id, 0L, 0L));
		}
		catch
		{
			await tx.RollbackAsync();
			throw;
		}
	}

}
