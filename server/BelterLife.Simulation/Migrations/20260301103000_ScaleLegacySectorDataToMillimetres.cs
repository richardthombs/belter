using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301103000_ScaleLegacySectorDataToMillimetres")]
    public partial class ScaleLegacySectorDataToMillimetres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
WITH legacy_sectors AS (
    SELECT DISTINCT sector_id
    FROM asteroids
    WHERE radius > 0 AND radius < 1000
)
UPDATE asteroids
SET x = x * 1000,
    y = y * 1000,
    radius = radius * 1000
WHERE sector_id IN (SELECT sector_id FROM legacy_sectors);

WITH legacy_sectors AS (
    SELECT sector_id
    FROM asteroids
    GROUP BY sector_id
    HAVING MIN(radius) >= 1000 AND MIN(radius) < 10000
)
UPDATE ships
SET x = x * 1000,
    y = y * 1000,
    velocity_x = velocity_x * 1000,
    velocity_y = velocity_y * 1000
WHERE sector_id IN (SELECT sector_id FROM legacy_sectors);

WITH legacy_sectors AS (
    SELECT sector_id
    FROM asteroids
    GROUP BY sector_id
    HAVING MIN(radius) >= 1000 AND MIN(radius) < 10000
)
UPDATE npc_stations
SET x = x * 1000,
    y = y * 1000
WHERE sector_id IN (SELECT sector_id FROM legacy_sectors);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
WITH scaled_legacy_sectors AS (
    SELECT sector_id
    FROM asteroids
    GROUP BY sector_id
    HAVING MIN(radius) >= 1000 AND MIN(radius) < 10000
)
UPDATE asteroids
SET x = x / 1000,
    y = y / 1000,
    radius = radius / 1000
WHERE sector_id IN (SELECT sector_id FROM scaled_legacy_sectors);

WITH scaled_legacy_sectors AS (
    SELECT sector_id
    FROM asteroids
    GROUP BY sector_id
    HAVING MIN(radius) > 0 AND MIN(radius) < 1000
)
UPDATE ships
SET x = x / 1000,
    y = y / 1000,
    velocity_x = velocity_x / 1000,
    velocity_y = velocity_y / 1000
WHERE sector_id IN (SELECT sector_id FROM scaled_legacy_sectors);

WITH scaled_legacy_sectors AS (
    SELECT sector_id
    FROM asteroids
    GROUP BY sector_id
    HAVING MIN(radius) > 0 AND MIN(radius) < 1000
)
UPDATE npc_stations
SET x = x / 1000,
    y = y / 1000
WHERE sector_id IN (SELECT sector_id FROM scaled_legacy_sectors);
");
        }
    }
}
