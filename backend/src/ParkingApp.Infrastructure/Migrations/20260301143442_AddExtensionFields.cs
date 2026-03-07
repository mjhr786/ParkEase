using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtensionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingExtensions");

            migrationBuilder.DropColumn(
                name: "CurrentExtensionId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "HasPendingExtension",
                table: "Bookings");

            migrationBuilder.AddColumn<decimal>(
                name: "PendingExtensionAmount",
                table: "Bookings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingExtensionEndDateTime",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreExtensionStatus",
                table: "Bookings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingExtensionAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PendingExtensionEndDateTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PreExtensionStatus",
                table: "Bookings");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentExtensionId",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPendingExtension",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BookingExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NewEndDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ServiceFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingExtensions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingExtensions_BookingId",
                table: "BookingExtensions",
                column: "BookingId");
        }
    }
}
