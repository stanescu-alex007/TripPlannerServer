using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowUnregisteredParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TripParticipants",
                table: "TripParticipants");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TripParticipants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "TripParticipants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "TripParticipants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TripParticipants",
                table: "TripParticipants",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TripParticipants_TripId_Email",
                table: "TripParticipants",
                columns: new[] { "TripId", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TripParticipants",
                table: "TripParticipants");

            migrationBuilder.DropIndex(
                name: "IX_TripParticipants_TripId_Email",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TripParticipants");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "TripParticipants");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TripParticipants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TripParticipants",
                table: "TripParticipants",
                columns: new[] { "TripId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TripParticipants_Users_UserId",
                table: "TripParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
