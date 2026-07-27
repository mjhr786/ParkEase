using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations;

/// <summary>
/// Chat inbox performance: composite participant+LastMessageAt indexes and backfill null LastMessageAt.
/// No API contract changes.
/// </summary>
public partial class AddConversationInboxIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Align sort column with CreatedAt so ORDER BY LastMessageAt matches prior COALESCE behavior.
        migrationBuilder.Sql(
            """
            UPDATE "Conversations"
            SET "LastMessageAt" = "CreatedAt"
            WHERE "LastMessageAt" IS NULL AND "IsDeleted" = false;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_UserId_LastMessageAt",
            table: "Conversations",
            columns: new[] { "UserId", "LastMessageAt" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_VendorId_LastMessageAt",
            table: "Conversations",
            columns: new[] { "VendorId", "LastMessageAt" },
            descending: new[] { false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Conversations_UserId_LastMessageAt",
            table: "Conversations");

        migrationBuilder.DropIndex(
            name: "IX_Conversations_VendorId_LastMessageAt",
            table: "Conversations");
    }
}
