using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class EventParkingPackageConfiguration : IEntityTypeConfiguration<EventParkingPackage>
{
    public void Configure(EntityTypeBuilder<EventParkingPackage> entity)
    {
        entity.ToTable("EventParkingPackages");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(2000);
        entity.Property(e => e.EventName).HasMaxLength(200);
        entity.Property(e => e.VenueName).HasMaxLength(200);
        entity.Property(e => e.ZoneName).HasMaxLength(100);
        entity.Property(e => e.PackagePrice).HasPrecision(18, 2);
        entity.Property(e => e.EarlyEntryMinutes).HasDefaultValue(0);
        entity.Property(e => e.LateExitMinutes).HasDefaultValue(0);
        entity.HasIndex(e => e.ParkingSpaceId);
        entity.HasIndex(e => e.VenueEventId);
        entity.HasIndex(e => new { e.IsActive, e.EventStartUtc });
        entity.HasIndex(e => new { e.SalesStartUtc, e.SalesEndUtc });
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.ParkingSpace)
            .WithMany()
            .HasForeignKey(e => e.ParkingSpaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
