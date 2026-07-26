using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAncillaryServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingAncillaryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAncillaryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingAncillaryLines_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParkingAncillaryServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingAncillaryServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingAncillaryServices_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingAncillaryLines_BookingId",
                table: "BookingAncillaryLines",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAncillaryLines_ServiceId",
                table: "BookingAncillaryLines",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAncillaryServices_ParkingSpaceId",
                table: "ParkingAncillaryServices",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAncillaryServices_ParkingSpaceId_IsActive_SortOrder",
                table: "ParkingAncillaryServices",
                columns: new[] { "ParkingSpaceId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingAncillaryLines");

            migrationBuilder.DropTable(
                name: "ParkingAncillaryServices");
        }
    }
}
