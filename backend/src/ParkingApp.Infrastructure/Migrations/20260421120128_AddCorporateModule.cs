using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BillingType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Companies_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    InvitationToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeInvitations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParkingAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalSlots = table.Column<int>(type: "integer", nullable: false),
                    FixedSlots = table.Column<int>(type: "integer", nullable: false),
                    SharedSlots = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaxBookingsPerDay = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    MaxBookingsPerWeek = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    PriorityThreshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    AllowedStartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    AllowedEndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    AllowWeekends = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingAllocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingAllocations_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingAllocations_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyMemberships_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCompanyMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BookingCount = table.Column<int>(type: "integer", nullable: false),
                    VisitorBookingCount = table.Column<int>(type: "integer", nullable: false),
                    TotalHoursUsed = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyUsages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyUsages_ParkingAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "ParkingAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    IsVisitorBooking = table.Column<bool>(type: "boolean", nullable: false),
                    VisitorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisitorLicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccessVehiclePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccessStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessExpiryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessQrToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateBookings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateBookings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateBookings_ParkingAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "ParkingAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorporateBookings_UserCompanyMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "UserCompanyMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedSlotAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedSlotAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedSlotAssignments_ParkingAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "ParkingAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedSlotAssignments_UserCompanyMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "UserCompanyMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_CreatedByUserId",
                table: "Companies",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_RegistrationNumber",
                table: "Companies",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsages_AllocationId",
                table: "CompanyUsages",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsages_CompanyId_AllocationId_UsageDate",
                table: "CompanyUsages",
                columns: new[] { "CompanyId", "AllocationId", "UsageDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsages_CompanyId_UsageDate",
                table: "CompanyUsages",
                columns: new[] { "CompanyId", "UsageDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateBookings_AllocationId",
                table: "CorporateBookings",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateBookings_BookingId",
                table: "CorporateBookings",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateBookings_CompanyId_CreatedAt",
                table: "CorporateBookings",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateBookings_MembershipId",
                table: "CorporateBookings",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvitations_CompanyId_Email",
                table: "EmployeeInvitations",
                columns: new[] { "CompanyId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvitations_InvitationToken",
                table: "EmployeeInvitations",
                column: "InvitationToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvitations_InvitedByUserId",
                table: "EmployeeInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedSlotAssignments_AllocationId_SlotNumber",
                table: "FixedSlotAssignments",
                columns: new[] { "AllocationId", "SlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedSlotAssignments_MembershipId",
                table: "FixedSlotAssignments",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_ApprovedByUserId",
                table: "ParkingAllocations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_CompanyId_ParkingSpaceId",
                table: "ParkingAllocations",
                columns: new[] { "CompanyId", "ParkingSpaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_CompanyId_Status",
                table: "ParkingAllocations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_ParkingSpaceId",
                table: "ParkingAllocations",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_Status",
                table: "ParkingAllocations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_CompanyId_IsActive",
                table: "UserCompanyMemberships",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_CompanyId_UserId",
                table: "UserCompanyMemberships",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyMemberships_UserId",
                table: "UserCompanyMemberships",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyUsages");

            migrationBuilder.DropTable(
                name: "CorporateBookings");

            migrationBuilder.DropTable(
                name: "EmployeeInvitations");

            migrationBuilder.DropTable(
                name: "FixedSlotAssignments");

            migrationBuilder.DropTable(
                name: "ParkingAllocations");

            migrationBuilder.DropTable(
                name: "UserCompanyMemberships");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
