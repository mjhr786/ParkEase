using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class LprCameraKeyConfiguration : IEntityTypeConfiguration<LprCameraKey>
{
    public void Configure(EntityTypeBuilder<LprCameraKey> entity)
    {
        entity.ToTable("LprCameraKeys");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.KeyId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.SecretHash).HasMaxLength(64).IsRequired();
        entity.Property(e => e.SecretPrefix).HasMaxLength(16).IsRequired();

        entity.HasIndex(e => e.KeyId).IsUnique().HasDatabaseName("IX_LprCameraKeys_KeyId");
        entity.HasIndex(e => e.SecretHash).HasDatabaseName("IX_LprCameraKeys_SecretHash");
        entity.HasIndex(e => e.ParkingSpaceId).HasDatabaseName("IX_LprCameraKeys_ParkingSpaceId");

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
