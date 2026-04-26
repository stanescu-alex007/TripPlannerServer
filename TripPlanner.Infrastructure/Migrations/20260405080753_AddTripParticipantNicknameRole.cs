using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripParticipantNicknameRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "TripParticipants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "TripParticipants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "TripParticipants");
        }
    }
}
