using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddP2PDrivewayListingCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InstantBook",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ListingCategory",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_InstantBook",
                table: "ParkingSpaces",
                column: "InstantBook");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_ListingCategory",
                table: "ParkingSpaces",
                column: "ListingCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_InstantBook",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_ListingCategory",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "InstantBook",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "ListingCategory",
                table: "ParkingSpaces");
        }
    }
}
