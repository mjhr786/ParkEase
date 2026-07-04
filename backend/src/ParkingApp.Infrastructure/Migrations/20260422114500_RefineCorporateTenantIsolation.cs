using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingApp.Infrastructure.Data;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260422114500_RefineCorporateTenantIsolation")]
public partial class RefineCorporateTenantIsolation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CompanyId",
            table: "FixedSlotAssignments",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "FixedSlotAssignments" f
            SET "CompanyId" = pa."CompanyId"
            FROM "ParkingAllocations" pa
            WHERE f."AllocationId" = pa."Id";
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "CompanyId",
            table: "FixedSlotAssignments",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_FixedSlotAssignments_AllocationId_SlotNumber",
            table: "FixedSlotAssignments");

        migrationBuilder.DropIndex(
            name: "IX_FixedSlotAssignments_MembershipId",
            table: "FixedSlotAssignments");

        migrationBuilder.CreateIndex(
            name: "IX_CorporateBookings_CompanyId_AllocationId_SlotType",
            table: "CorporateBookings",
            columns: new[] { "CompanyId", "AllocationId", "SlotType" });

        migrationBuilder.CreateIndex(
            name: "IX_CorporateBookings_CompanyId_MembershipId_CreatedAt",
            table: "CorporateBookings",
            columns: new[] { "CompanyId", "MembershipId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_FixedSlotAssignments_CompanyId_AllocationId_SlotNumber",
            table: "FixedSlotAssignments",
            columns: new[] { "CompanyId", "AllocationId", "SlotNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FixedSlotAssignments_CompanyId_MembershipId",
            table: "FixedSlotAssignments",
            columns: new[] { "CompanyId", "MembershipId" });

        migrationBuilder.CreateIndex(
            name: "IX_ParkingAllocations_CompanyId_Status_CreatedAt",
            table: "ParkingAllocations",
            columns: new[] { "CompanyId", "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserCompanyMemberships_CompanyId_Role_CreatedAt",
            table: "UserCompanyMemberships",
            columns: new[] { "CompanyId", "Role", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserCompanyMemberships_CompanyId_Role_IsActive",
            table: "UserCompanyMemberships",
            columns: new[] { "CompanyId", "Role", "IsActive" });

        migrationBuilder.AddForeignKey(
            name: "FK_FixedSlotAssignments_Companies_CompanyId",
            table: "FixedSlotAssignments",
            column: "CompanyId",
            principalTable: "Companies",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_FixedSlotAssignments_Companies_CompanyId",
            table: "FixedSlotAssignments");

        migrationBuilder.DropIndex(
            name: "IX_CorporateBookings_CompanyId_AllocationId_SlotType",
            table: "CorporateBookings");

        migrationBuilder.DropIndex(
            name: "IX_CorporateBookings_CompanyId_MembershipId_CreatedAt",
            table: "CorporateBookings");

        migrationBuilder.DropIndex(
            name: "IX_FixedSlotAssignments_CompanyId_AllocationId_SlotNumber",
            table: "FixedSlotAssignments");

        migrationBuilder.DropIndex(
            name: "IX_FixedSlotAssignments_CompanyId_MembershipId",
            table: "FixedSlotAssignments");

        migrationBuilder.DropIndex(
            name: "IX_ParkingAllocations_CompanyId_Status_CreatedAt",
            table: "ParkingAllocations");

        migrationBuilder.DropIndex(
            name: "IX_UserCompanyMemberships_CompanyId_Role_CreatedAt",
            table: "UserCompanyMemberships");

        migrationBuilder.DropIndex(
            name: "IX_UserCompanyMemberships_CompanyId_Role_IsActive",
            table: "UserCompanyMemberships");

        migrationBuilder.CreateIndex(
            name: "IX_FixedSlotAssignments_AllocationId_SlotNumber",
            table: "FixedSlotAssignments",
            columns: new[] { "AllocationId", "SlotNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FixedSlotAssignments_MembershipId",
            table: "FixedSlotAssignments",
            column: "MembershipId");

        migrationBuilder.DropColumn(
            name: "CompanyId",
            table: "FixedSlotAssignments");
    }
}
