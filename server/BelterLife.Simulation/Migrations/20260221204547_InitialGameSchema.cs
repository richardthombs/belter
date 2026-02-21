using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "npc_stations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_stations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    ship_id = table.Column<int>(type: "integer", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sectors",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sectors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ships",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    velocity_x = table.Column<float>(type: "real", nullable: false),
                    velocity_y = table.Column<float>(type: "real", nullable: false),
                    heading = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asteroids",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sector_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    radius = table.Column<float>(type: "real", nullable: false),
                    vertex_count = table.Column<int>(type: "integer", nullable: false),
                    rotation_offset = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asteroids", x => x.id);
                    table.ForeignKey(
                        name: "fk_asteroids_sectors_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asteroids_sector_id",
                table: "asteroids",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_npc_stations_sector_id",
                table: "npc_stations",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_ships_player_id",
                table: "ships",
                column: "player_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "asteroids");

            migrationBuilder.DropTable(
                name: "npc_stations");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "ships");

            migrationBuilder.DropTable(
                name: "sectors");
        }
    }
}
