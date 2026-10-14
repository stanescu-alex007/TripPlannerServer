using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TripDeletionBin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Archiving is gone: a trip nobody took over is simply deleted. Existing
            // archived trips are converted rather than dropped on the floor, or they
            // would come back as ownerless trips nobody can act on. Children first,
            // while the column they read is still there.
            migrationBuilder.Sql(
                """
                UPDATE "TripParticipants" p
                   SET "DeletedAt" = t."ArchivedAt", "UpdatedAt" = now()
                  FROM "Trips" t
                 WHERE p."TripId" = t."Id" AND t."ArchivedAt" IS NOT NULL AND p."DeletedAt" IS NULL;

                UPDATE "TripScheduleItems" s
                   SET "DeletedAt" = t."ArchivedAt", "UpdatedAt" = now()
                  FROM "Trips" t
                 WHERE s."TripId" = t."Id" AND t."ArchivedAt" IS NOT NULL AND s."DeletedAt" IS NULL;

                UPDATE "Trips"
                   SET "DeletedAt" = "ArchivedAt", "UpdatedAt" = now()
                 WHERE "ArchivedAt" IS NOT NULL AND "DeletedAt" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Trips");

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DeletedByUserId_DeletedAt",
                table: "Trips",
                columns: new[] { "DeletedByUserId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_DeletedByUserId_DeletedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Trips");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
