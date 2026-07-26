using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvChargingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvChargerCount",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EvChargingRatePerHour",
                table: "ParkingSpaces",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EvIdleGraceMinutes",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EvIdleRatePerHour",
                table: "ParkingSpaces",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "HasEvCharging",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "EvChargingFeeAmount",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EvIdleFeeAmount",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvIdleFeeChargedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeEvCharging",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_HasEvCharging",
                table: "ParkingSpaces",
                column: "HasEvCharging");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_HasEvCharging",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvChargerCount",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvChargingRatePerHour",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvIdleGraceMinutes",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvIdleRatePerHour",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "HasEvCharging",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "EvChargingFeeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EvIdleFeeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EvIdleFeeChargedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IncludeEvCharging",
                table: "Bookings");
        }
    }
}
