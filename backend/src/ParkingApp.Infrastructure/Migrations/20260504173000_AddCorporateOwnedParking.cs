using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    public partial class AddCorporateOwnedParking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyOwnerId",
                table: "ParkingSpaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorporateOnly",
                table: "ParkingSpaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OwnershipType",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LeaseReference",
                table: "ParkingAllocations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                table: "ParkingAllocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_CompanyOwnerId",
                table: "ParkingSpaces",
                column: "CompanyOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_OwnershipType",
                table: "ParkingSpaces",
                column: "OwnershipType");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_CompanyId_SourceType_Status",
                table: "ParkingAllocations",
                columns: new[] { "CompanyId", "SourceType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_VendorId",
                table: "ParkingAllocations",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSpaces_Companies_CompanyOwnerId",
                table: "ParkingSpaces",
                column: "CompanyOwnerId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSpaces_Companies_CompanyOwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_CompanyOwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_OwnershipType",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingAllocations_CompanyId_SourceType_Status",
                table: "ParkingAllocations");

            migrationBuilder.DropIndex(
                name: "IX_ParkingAllocations_VendorId",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "CompanyOwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "IsCorporateOnly",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "OwnershipType",
                table: "ParkingSpaces");

            migrationBuilder.DropColumn(
                name: "LeaseReference",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ParkingAllocations");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "ParkingAllocations");
        }
    }
}
