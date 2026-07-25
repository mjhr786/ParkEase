using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.OwnerResponse).HasMaxLength(1000);
            entity.HasIndex(e => e.ParkingSpaceId);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            // UserId is ID-centric; DB FK to Users remains from migrations.
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Reviews)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
    }
}
