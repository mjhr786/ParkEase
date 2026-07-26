using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class LprAccessAttemptConfiguration : IEntityTypeConfiguration<LprAccessAttempt>
{
    public void Configure(EntityTypeBuilder<LprAccessAttempt> entity)
    {
        entity.ToTable("LprAccessAttempts");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.LicensePlateRaw).HasMaxLength(50).IsRequired();
        entity.Property(e => e.LicensePlateNormalized).HasMaxLength(20).IsRequired();
        entity.Property(e => e.DenialReason).HasMaxLength(64);
        entity.Property(e => e.Source).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ClientKeyId).HasMaxLength(64);
        entity.Property(e => e.ImageUrl).HasMaxLength(1000);

        entity.HasIndex(e => new { e.ParkingSpaceId, e.OccurredAtUtc })
            .HasDatabaseName("IX_LprAccessAttempts_Space_Occurred");
        entity.HasIndex(e => new { e.LicensePlateNormalized, e.OccurredAtUtc })
            .HasDatabaseName("IX_LprAccessAttempts_Plate_Occurred");
        entity.HasIndex(e => e.BookingId)
            .HasDatabaseName("IX_LprAccessAttempts_BookingId");

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
