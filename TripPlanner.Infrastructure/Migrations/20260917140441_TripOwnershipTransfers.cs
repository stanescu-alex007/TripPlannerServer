using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TripOwnershipTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Trips",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TripOwnershipTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripOwnershipTransfers", x => x.Id);
                    table.CheckConstraint("CK_TripOwnershipTransfers_Status", "\"Status\" IN ('Pending','Accepted','Declined','Expired','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_TripOwnershipTransfers_TripParticipants_CandidateParticipan~",
                        column: x => x.CandidateParticipantId,
                        principalTable: "TripParticipants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripOwnershipTransfers_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripOwnershipTransfers_CandidateParticipantId",
                table: "TripOwnershipTransfers",
                column: "CandidateParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TripOwnershipTransfers_OnePendingPerTrip",
                table: "TripOwnershipTransfers",
                column: "TripId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_TripOwnershipTransfers_Status_ExpiresAt",
                table: "TripOwnershipTransfers",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TripOwnershipTransfers_TripId",
                table: "TripOwnershipTransfers",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripOwnershipTransfers");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Trips");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Trips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
