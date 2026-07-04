using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateWaitlistEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorporateWaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsVisitorBooking = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedStartDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedEndDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    VehicleNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VisitorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisitorLicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccessExpiryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PriorityAtRequest = table.Column<int>(type: "integer", nullable: false),
                    PromotedBookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateWaitlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateWaitlistEntries_Bookings_PromotedBookingId",
                        column: x => x.PromotedBookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateWaitlistEntries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateWaitlistEntries_ParkingAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "ParkingAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateWaitlistEntries_UserCompanyMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "UserCompanyMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_FixedSlotAssignments_AllocationId"" ON ""FixedSlotAssignments"" (""AllocationId"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_FixedSlotAssignments_MembershipId"" ON ""FixedSlotAssignments"" (""MembershipId"");");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_AllocationId",
                table: "CorporateWaitlistEntries",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_CompanyId_AllocationId_RequestedSt~",
                table: "CorporateWaitlistEntries",
                columns: new[] { "CompanyId", "AllocationId", "RequestedStartDateTime", "RequestedEndDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_CompanyId_AllocationId_Status_Prio~",
                table: "CorporateWaitlistEntries",
                columns: new[] { "CompanyId", "AllocationId", "Status", "PriorityAtRequest", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_CompanyId_MembershipId_Status",
                table: "CorporateWaitlistEntries",
                columns: new[] { "CompanyId", "MembershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_MembershipId",
                table: "CorporateWaitlistEntries",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_PromotedBookingId",
                table: "CorporateWaitlistEntries",
                column: "PromotedBookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorporateWaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_FixedSlotAssignments_AllocationId",
                table: "FixedSlotAssignments");

            migrationBuilder.DropIndex(
                name: "IX_FixedSlotAssignments_MembershipId",
                table: "FixedSlotAssignments");
        }
    }
}
