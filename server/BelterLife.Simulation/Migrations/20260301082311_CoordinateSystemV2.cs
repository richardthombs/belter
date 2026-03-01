using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    /// <inheritdoc />
    public partial class CoordinateSystemV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "grid_x",
                table: "sectors",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "grid_y",
                table: "sectors",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "is_generated",
                table: "sectors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("ALTER TABLE asteroids ALTER COLUMN x TYPE bigint USING x::bigint");
            migrationBuilder.Sql("ALTER TABLE asteroids ALTER COLUMN y TYPE bigint USING y::bigint");
            migrationBuilder.Sql("ALTER TABLE ships ALTER COLUMN x TYPE bigint USING x::bigint");
            migrationBuilder.Sql("ALTER TABLE ships ALTER COLUMN y TYPE bigint USING y::bigint");
            migrationBuilder.Sql("ALTER TABLE npc_stations ALTER COLUMN x TYPE bigint USING x::bigint");
            migrationBuilder.Sql("ALTER TABLE npc_stations ALTER COLUMN y TYPE bigint USING y::bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grid_x",
                table: "sectors");

            migrationBuilder.DropColumn(
                name: "grid_y",
                table: "sectors");

            migrationBuilder.DropColumn(
                name: "is_generated",
                table: "sectors");

            migrationBuilder.AlterColumn<float>(
                name: "y",
                table: "ships",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<float>(
                name: "x",
                table: "ships",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<float>(
                name: "y",
                table: "npc_stations",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<float>(
                name: "x",
                table: "npc_stations",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<float>(
                name: "y",
                table: "asteroids",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<float>(
                name: "x",
                table: "asteroids",
                type: "real",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
