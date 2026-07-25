using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class ParkingAvailabilityConfiguration : IEntityTypeConfiguration<ParkingAvailability>
{
    public void Configure(EntityTypeBuilder<ParkingAvailability> entity)
    {
entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParkingSpaceId, e.Date });
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Availabilities)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
    }
}
