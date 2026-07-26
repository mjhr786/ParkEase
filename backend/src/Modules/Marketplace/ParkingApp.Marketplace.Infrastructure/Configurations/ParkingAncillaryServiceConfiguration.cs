using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class ParkingAncillaryServiceConfiguration : IEntityTypeConfiguration<ParkingAncillaryService>
{
    public void Configure(EntityTypeBuilder<ParkingAncillaryService> entity)
    {
        entity.ToTable("ParkingAncillaryServices");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.HasIndex(e => e.ParkingSpaceId);
        entity.HasIndex(e => new { e.ParkingSpaceId, e.IsActive, e.SortOrder });
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.ParkingSpace)
            .WithMany()
            .HasForeignKey(e => e.ParkingSpaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
