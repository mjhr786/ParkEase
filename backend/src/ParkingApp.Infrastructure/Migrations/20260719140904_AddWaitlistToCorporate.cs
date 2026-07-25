using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistToCorporate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_CreatedByUserId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_CorporateBookings_Bookings_BookingId",
                table: "CorporateBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CorporateWaitlistEntries_Bookings_PromotedBookingId",
                table: "CorporateWaitlistEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeInvitations_Users_InvitedByUserId",
                table: "EmployeeInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingAllocations_ParkingSpaces_ParkingSpaceId",
                table: "ParkingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingAllocations_Users_ApprovedByUserId",
                table: "ParkingAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSpaces_Companies_CompanyOwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropForeignKey(
                name: "FK_ParkingSpaces_Users_OwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCompanyMemberships_Users_UserId",
                table: "UserCompanyMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ParkingSpaces_OwnerId",
                table: "ParkingSpaces");

            migrationBuilder.DropIndex(
                name: "IX_ParkingAllocations_ApprovedByUserId",
                table: "ParkingAllocations");

            migrationBuilder.DropIndex(
                name: "IX_ParkingAllocations_ParkingSpaceId",
                table: "ParkingAllocations");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeInvitations_InvitedByUserId",
                table: "EmployeeInvitations");

            migrationBuilder.DropIndex(
                name: "IX_CorporateWaitlistEntries_PromotedBookingId",
                table: "CorporateWaitlistEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_OwnerId",
                table: "ParkingSpaces",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_ApprovedByUserId",
                table: "ParkingAllocations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingAllocations_ParkingSpaceId",
                table: "ParkingAllocations",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeInvitations_InvitedByUserId",
                table: "EmployeeInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateWaitlistEntries_PromotedBookingId",
                table: "CorporateWaitlistEntries",
                column: "PromotedBookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_CreatedByUserId",
                table: "Companies",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CorporateBookings_Bookings_BookingId",
                table: "CorporateBookings",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CorporateWaitlistEntries_Bookings_PromotedBookingId",
                table: "CorporateWaitlistEntries",
                column: "PromotedBookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeInvitations_Users_InvitedByUserId",
                table: "EmployeeInvitations",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingAllocations_ParkingSpaces_ParkingSpaceId",
                table: "ParkingAllocations",
                column: "ParkingSpaceId",
                principalTable: "ParkingSpaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingAllocations_Users_ApprovedByUserId",
                table: "ParkingAllocations",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSpaces_Companies_CompanyOwnerId",
                table: "ParkingSpaces",
                column: "CompanyOwnerId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingSpaces_Users_OwnerId",
                table: "ParkingSpaces",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCompanyMemberships_Users_UserId",
                table: "UserCompanyMemberships",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
