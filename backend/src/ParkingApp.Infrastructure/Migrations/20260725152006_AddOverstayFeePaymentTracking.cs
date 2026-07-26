using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOverstayFeePaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OverstayFeePaidAmount",
                table: "Bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverstayFeePaidAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverstayFeeTransactionId",
                table: "Bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverstayFeePaidAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OverstayFeePaidAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OverstayFeeTransactionId",
                table: "Bookings");
        }
    }
}
