using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvChargingKwhSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvPricingMode",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EvRatePerKwh",
                table: "ParkingSpaces",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "EvChargingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    OcppTransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StoppedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MeterStartKwh = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LastMeterKwh = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    MeterEndKwh = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    EnergyDeliveredKwh = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RatePerKwh = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnergyFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvChargingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvChargingSessions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvChargingSessions_Booking_Started",
                table: "EvChargingSessions",
                columns: new[] { "BookingId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EvChargingSessions_OcppTransactionId",
                table: "EvChargingSessions",
                column: "OcppTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvChargingSessions_ParkingSpaceId",
                table: "EvChargingSessions",
                column: "ParkingSpaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvChargingSessions");

            migrationBuilder.DropColumn(
                name: "EvPricingMode",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvRatePerKwh",
                table: "ParkingSpaces");
        }
    }
}
