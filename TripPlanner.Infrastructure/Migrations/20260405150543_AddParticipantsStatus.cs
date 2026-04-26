using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantsStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TripParticipants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TripParticipants");
        }
    }
}
