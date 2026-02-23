using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelterLife.Simulation.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "credits",
                table: "players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "credits",
                table: "players");
        }
    }
}
