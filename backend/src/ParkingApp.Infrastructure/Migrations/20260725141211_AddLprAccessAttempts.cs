using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLprAccessAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LprAccessAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlateRaw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LicensePlateNormalized = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    DenialReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientKeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LprAccessAttempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LprAccessAttempts_BookingId",
                table: "LprAccessAttempts",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_LprAccessAttempts_Plate_Occurred",
                table: "LprAccessAttempts",
                columns: new[] { "LicensePlateNormalized", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LprAccessAttempts_Space_Occurred",
                table: "LprAccessAttempts",
                columns: new[] { "ParkingSpaceId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LprAccessAttempts");
        }
    }
}
