using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Infrastructure.Configurations;

/// <summary>
/// Conversation is ID-centric; FK columns are indexed without compile-time references
/// to Identity/Marketplace entity types (DB FKs remain from existing migrations).
/// </summary>
internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LastMessagePreview).HasMaxLength(100);
        entity.HasIndex(e => new { e.ParkingSpaceId, e.UserId }).IsUnique();
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.VendorId);
        entity.HasIndex(e => e.LastMessageAt);
        entity.HasQueryFilter(e => !e.IsDeleted);

        // Identity User / Marketplace ParkingSpace principals are configured in their modules.
        // Cross-module navigations avoided so Messaging.Infrastructure stays free of foreign Domain projects.
    }
}
