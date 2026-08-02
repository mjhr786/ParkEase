using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingApp.Infrastructure.Data;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260802120000_AddCorporateVehicleClassPools")]
    public partial class AddCorporateVehicleClassPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TwoWheelerTotalSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TwoWheelerFixedSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TwoWheelerSharedSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FourWheelerTotalSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FourWheelerFixedSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FourWheelerSharedSlots",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: historical homogeneous inventory → FourWheeler
            migrationBuilder.Sql("""
                UPDATE "ParkingAllocations"
                SET
                    "FourWheelerTotalSlots" = "TotalSlots",
                    "FourWheelerFixedSlots" = "FixedSlots",
                    "FourWheelerSharedSlots" = "SharedSlots",
                    "TwoWheelerTotalSlots" = 0,
                    "TwoWheelerFixedSlots" = 0,
                    "TwoWheelerSharedSlots" = 0;
                """);

            migrationBuilder.AddColumn<int>(
                name: "VehicleClass",
                table: "FixedSlotAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 2); // FourWheeler

            migrationBuilder.DropIndex(
                name: "IX_FixedSlotAssignments_CompanyId_AllocationId_SlotNumber",
                table: "FixedSlotAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_FixedSlotAssignments_CompanyId_AllocationId_VehicleClass_SlotNumber",
                table: "FixedSlotAssignments",
                columns: new[] { "CompanyId", "AllocationId", "VehicleClass", "SlotNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixedSlotAssignments_CompanyId_AllocationId_VehicleClass_SlotNumber",
                table: "FixedSlotAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_FixedSlotAssignments_CompanyId_AllocationId_SlotNumber",
                table: "FixedSlotAssignments",
                columns: new[] { "CompanyId", "AllocationId", "SlotNumber" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                table: "FixedSlotAssignments");

            migrationBuilder.DropColumn(
                name: "TwoWheelerTotalSlots",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "TwoWheelerFixedSlots",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "TwoWheelerSharedSlots",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "FourWheelerTotalSlots",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "FourWheelerFixedSlots",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "FourWheelerSharedSlots",
                table: "ParkingAllocations");
        }
    }
}
