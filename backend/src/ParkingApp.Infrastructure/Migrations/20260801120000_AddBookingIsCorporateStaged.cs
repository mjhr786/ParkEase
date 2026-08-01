using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations;

/// <summary>
/// KD-19: Marketplace-owned flag so consumer My Bookings can exclude corporate-staged rows
/// without SQL anti-join on CorporateBookings.
/// </summary>
public partial class AddBookingIsCorporateStaged : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCorporateStaged",
            table: "Bookings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_IsCorporateStaged",
            table: "Bookings",
            column: "IsCorporateStaged");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Bookings_IsCorporateStaged",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "IsCorporateStaged",
            table: "Bookings");
    }
}
