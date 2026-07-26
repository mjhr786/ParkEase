using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSpaceDynamicPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DynamicMaxMultiplier",
                table: "ParkingSpaces",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 1.75m);

            migrationBuilder.AddColumn<decimal>(
                name: "DynamicMinMultiplier",
                table: "ParkingSpaces",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0.80m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDynamicPricingEnabled",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PeakHourMultiplier",
                table: "ParkingSpaces",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 1.25m);

            migrationBuilder.AddColumn<decimal>(
                name: "WeekendMultiplier",
                table: "ParkingSpaces",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 1.15m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DynamicMaxMultiplier",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "DynamicMinMultiplier",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IsDynamicPricingEnabled",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "PeakHourMultiplier",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "WeekendMultiplier",
                table: "ParkingSpaces");
        }
    }
}
