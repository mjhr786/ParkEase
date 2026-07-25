using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Infrastructure.Configurations;

internal sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DeviceId).HasMaxLength(500).IsRequired();
        entity.Property(e => e.Platform).HasMaxLength(20).IsRequired();
        entity.Property(e => e.FcmToken).HasMaxLength(1000).IsRequired();
        entity.Property(e => e.AppVersion).HasMaxLength(50);
        entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
        entity.HasIndex(e => e.FcmToken);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
