using BelterLife.Simulation.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301120000_AddAsteroidDynamicState")]
    public partial class AddAsteroidDynamicState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_destroyed",
                table: "asteroids",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "velocity_x",
                table: "asteroids",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "velocity_y",
                table: "asteroids",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_destroyed",
                table: "asteroids");

            migrationBuilder.DropColumn(
                name: "velocity_x",
                table: "asteroids");

            migrationBuilder.DropColumn(
                name: "velocity_y",
                table: "asteroids");
        }
    }
}
