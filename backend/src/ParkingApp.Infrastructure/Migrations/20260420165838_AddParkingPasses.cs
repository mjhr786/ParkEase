using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZoneCode",
                table: "ParkingSpaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParkingPassId",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParkingPasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParkingZoneCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AllocatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorporateBatchReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CoverageType = table.Column<int>(type: "integer", nullable: false),
                    PassType = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsageMode = table.Column<int>(type: "integer", nullable: false),
                    DailyHourLimit = table.Column<int>(type: "integer", nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingPasses_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ParkingPasses_Users_AllocatedByUserId",
                        column: x => x.AllocatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingPasses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_ZoneCode",
                table: "ParkingSpaces",
                column: "ZoneCode");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ParkingPassId",
                table: "Bookings",
                column: "ParkingPassId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingPasses_AllocatedByUserId",
                table: "ParkingPasses",
                column: "AllocatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingPasses_CreatedAt_UserId",
                table: "ParkingPasses",
                columns: new[] { "CreatedAt", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingPasses_ParkingSpaceId",
                table: "ParkingPasses",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingPasses_UserId_ParkingSpaceId",
                table: "ParkingPasses",
                columns: new[] { "UserId", "ParkingSpaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingPasses_UserId_ParkingZoneCode",
                table: "ParkingPasses",
                columns: new[] { "UserId", "ParkingZoneCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ParkingPasses_ParkingPassId",
                table: "Bookings",
                column: "ParkingPassId",
                principalTable: "ParkingPasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ParkingPasses_ParkingPassId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "ParkingPasses");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_ZoneCode",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ParkingPassId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ZoneCode",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "ParkingPassId",
                table: "Bookings");
        }
    }
}
