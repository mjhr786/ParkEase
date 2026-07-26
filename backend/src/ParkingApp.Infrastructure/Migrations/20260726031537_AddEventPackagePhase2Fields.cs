using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPackagePhase2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyEntryMinutes",
                table: "EventParkingPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateExitMinutes",
                table: "EventParkingPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Temporary default so non-null add succeeds; backfill each row to its own Id next.
            migrationBuilder.AddColumn<Guid>(
                name: "VenueEventId",
                table: "EventParkingPackages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Phase 1 packages become single-lot venue groups (not one shared empty Guid).
            migrationBuilder.Sql("""UPDATE "EventParkingPackages" SET "VenueEventId" = "Id" WHERE "VenueEventId" = '00000000-0000-0000-0000-000000000000';""");

            migrationBuilder.AddColumn<string>(
                name: "ZoneName",
                table: "EventParkingPackages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventParkingPackages_VenueEventId",
                table: "EventParkingPackages",
                column: "VenueEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventParkingPackages_VenueEventId",
                table: "EventParkingPackages");

            migrationBuilder.DropColumn(
                name: "EarlyEntryMinutes",
                table: "EventParkingPackages");

            migrationBuilder.DropColumn(
                name: "LateExitMinutes",
                table: "EventParkingPackages");

            migrationBuilder.DropColumn(
                name: "VenueEventId",
                table: "EventParkingPackages");

            migrationBuilder.DropColumn(
                name: "ZoneName",
                table: "EventParkingPackages");
        }
    }
}
