using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLprP2OverstayConfidenceFuzzy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Confidence",
                table: "LprAccessAttempts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "LprAccessAttempts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverstayNotifiedAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "LprAccessAttempts");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "LprAccessAttempts");

            migrationBuilder.DropColumn(
                name: "OverstayNotifiedAt",
                table: "Bookings");
        }
    }
}
