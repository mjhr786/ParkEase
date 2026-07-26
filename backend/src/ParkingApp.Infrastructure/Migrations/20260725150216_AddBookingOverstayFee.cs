using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingOverstayFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OverstayBillableMinutes",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OverstayFeeAmount",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverstayFeeChargedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverstayBillableMinutes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OverstayFeeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OverstayFeeChargedAt",
                table: "Bookings");
        }
    }
}
