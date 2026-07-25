using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Chat unread-query performance index only.
    /// Broader model drift (cross-module FK removals for modular monolith) is intentionally
    /// not applied here so production referential constraints stay intact until a dedicated migration.
    /// </remarks>
    public partial class AddChatMessageUnreadIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId_IsRead_SenderId",
                table: "ChatMessages",
                columns: new[] { "ConversationId", "IsRead", "SenderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId_IsRead_SenderId",
                table: "ChatMessages");
        }
    }
}
