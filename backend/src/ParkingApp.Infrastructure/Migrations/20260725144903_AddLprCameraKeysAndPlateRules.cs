using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLprCameraKeysAndPlateRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LprCameraKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecretPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LprCameraKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LprPlateRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePlateNormalized = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LprPlateRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LprCameraKeys_KeyId",
                table: "LprCameraKeys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LprCameraKeys_ParkingSpaceId",
                table: "LprCameraKeys",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_LprCameraKeys_SecretHash",
                table: "LprCameraKeys",
                column: "SecretHash");

            migrationBuilder.CreateIndex(
                name: "IX_LprPlateRules_ParkingSpaceId",
                table: "LprPlateRules",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_LprPlateRules_Space_Plate_Type",
                table: "LprPlateRules",
                columns: new[] { "ParkingSpaceId", "LicensePlateNormalized", "RuleType" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LprCameraKeys");

            migrationBuilder.DropTable(
                name: "LprPlateRules");
        }
    }
}
