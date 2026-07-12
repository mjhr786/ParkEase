using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId1",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId1",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_PublicActive",
                table: "ParkingSpaces",
                columns: new[] { "City", "CreatedAt" },
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false AND \"IsCorporateOnly\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Pending_Space",
                table: "Bookings",
                columns: new[] { "ParkingSpaceId", "CreatedAt" },
                filter: "\"IsDeleted\" = false AND \"Status\" IN (0, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Space_ActiveWindow",
                table: "Bookings",
                columns: new[] { "ParkingSpaceId", "StartDateTime", "EndDateTime" },
                filter: "\"IsDeleted\" = false AND \"Status\" NOT IN (4, 5, 7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_PublicActive",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Pending_Space",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Space_ActiveWindow",
                table: "Bookings");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId1",
                table: "Notifications",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId1",
                table: "Notifications",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
