using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingApp.Infrastructure.Data;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations;

/// <summary>
/// KD-19: Marketplace-owned flag so consumer My Bookings can exclude corporate-staged rows
/// without SQL anti-join on CorporateBookings in Marketplace.Infrastructure.
/// Backfills existing CorporateBookings-linked rows at the host migration layer.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801120000_AddBookingIsCorporateStaged")]
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

        // Historical corporate-staged marketplace bookings (host layer; not Marketplace preferred path).
        migrationBuilder.Sql(
            """
            UPDATE "Bookings" b
            SET "IsCorporateStaged" = TRUE
            FROM "CorporateBookings" cb
            WHERE cb."BookingId" = b."Id"
              AND b."IsCorporateStaged" = FALSE
              AND b."IsDeleted" = FALSE
              AND cb."IsDeleted" = FALSE;
            """);

        // Composite matches consumer list filter: UserId + IsCorporateStaged (boolean-only index is low selectivity).
        migrationBuilder.CreateIndex(
            name: "IX_Bookings_UserId_IsCorporateStaged",
            table: "Bookings",
            columns: new[] { "UserId", "IsCorporateStaged" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Bookings_UserId_IsCorporateStaged",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "IsCorporateStaged",
            table: "Bookings");
    }
}
