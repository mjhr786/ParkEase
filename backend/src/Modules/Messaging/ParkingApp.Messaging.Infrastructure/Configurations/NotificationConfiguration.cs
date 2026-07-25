using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Infrastructure.Configurations;

/// <summary>
/// Notification is ID-centric (<see cref="Notification.UserId"/>); no User reverse collection.
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();

        entity.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("IX_Notifications_UserId_CreatedAt")
            .HasFilter("\"IsDeleted\" = false");
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
