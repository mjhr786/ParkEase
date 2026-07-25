using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> entity)
    {
entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ParkingSpaceId }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);

            // UserId is ID-centric; DB FK to Users remains from migrations.

            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.FavoritedBy)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
    }
}
