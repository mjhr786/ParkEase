using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndoorBayAndValetSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultFacilityLevel",
                table: "ParkingSpaces",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultFacilityZone",
                table: "ParkingSpaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndoorGuidanceNotes",
                table: "ParkingSpaces",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBayGuidanceEnabled",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsValetEnabled",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BayLabel",
                table: "Bookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityLevel",
                table: "Bookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacilityZone",
                table: "Bookings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValetNotes",
                table: "Bookings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValetReadyAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValetRequestedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValetStaffNotifiedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValetStatus",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValetTargetReadyAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_IsBayGuidanceEnabled",
                table: "ParkingSpaces",
                column: "IsBayGuidanceEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_IsValetEnabled",
                table: "ParkingSpaces",
                column: "IsValetEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ValetStatus",
                table: "Bookings",
                column: "ValetStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_IsBayGuidanceEnabled",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_IsValetEnabled",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ValetStatus",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DefaultFacilityLevel",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "DefaultFacilityZone",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IndoorGuidanceNotes",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IsBayGuidanceEnabled",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IsValetEnabled",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "BayLabel",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FacilityLevel",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FacilityZone",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetNotes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetReadyAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetRequestedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetStaffNotifiedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetStatus",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValetTargetReadyAt",
                table: "Bookings");
        }
    }
}
