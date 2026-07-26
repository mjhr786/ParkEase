using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventParkingPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventParkingPackageId",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventParkingPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VenueName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PackagePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSpots = table.Column<int>(type: "integer", nullable: false),
                    SoldCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventParkingPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventParkingPackages_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_EventParkingPackageId",
                table: "Bookings",
                column: "EventParkingPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParkingPackages_IsActive_EventStartUtc",
                table: "EventParkingPackages",
                columns: new[] { "IsActive", "EventStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventParkingPackages_ParkingSpaceId",
                table: "EventParkingPackages",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_EventParkingPackages_SalesStartUtc_SalesEndUtc",
                table: "EventParkingPackages",
                columns: new[] { "SalesStartUtc", "SalesEndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventParkingPackages");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_EventParkingPackageId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EventParkingPackageId",
                table: "Bookings");
        }
    }
}
