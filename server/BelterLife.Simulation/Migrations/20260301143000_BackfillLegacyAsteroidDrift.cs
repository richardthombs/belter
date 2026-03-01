using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301143000_BackfillLegacyAsteroidDrift")]
    public partial class BackfillLegacyAsteroidDrift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
UPDATE asteroids
SET
    velocity_x = (
        COS(ATAN2(y::double precision, x::double precision) + (PI() / 2.0))
        * (250.0 + MOD(((id * 1664525)::bigint + (sector_id * 69069)::bigint + 1013904223), 1501)::double precision)
    )::real,
    velocity_y = (
        SIN(ATAN2(y::double precision, x::double precision) + (PI() / 2.0))
        * (250.0 + MOD(((id * 1664525)::bigint + (sector_id * 69069)::bigint + 1013904223), 1501)::double precision)
    )::real
WHERE is_destroyed = FALSE
  AND velocity_x = 0
  AND velocity_y = 0;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- no-op: data backfill intentionally not reversed");
        }
    }
}
