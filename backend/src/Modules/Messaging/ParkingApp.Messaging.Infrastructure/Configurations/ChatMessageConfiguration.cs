using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Infrastructure.Configurations;

/// <summary>
/// ChatMessage is ID-centric for sender; conversation navigation stays within Messaging.
/// </summary>
internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
        entity.HasIndex(e => e.ConversationId);
        entity.HasIndex(e => e.SenderId);
        entity.HasIndex(e => new { e.ConversationId, e.CreatedAt });
        // Speeds unread COUNTs and mark-as-read filters: (conversation, isRead, sender)
        entity.HasIndex(e => new { e.ConversationId, e.IsRead, e.SenderId })
            .HasDatabaseName("IX_ChatMessages_ConversationId_IsRead_SenderId");
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
