using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ParkingApp.Infrastructure.Data;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260302093000_AddBookingSlotNumber")]
    public partial class AddBookingSlotNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotNumber",
                table: "Bookings",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotNumber",
                table: "Bookings");
        }
    }
}
