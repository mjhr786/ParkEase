using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Infrastructure.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LicensePlate).HasMaxLength(20).IsRequired();
        entity.Property(e => e.Make).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Model).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Color).HasMaxLength(50).IsRequired();
        entity.HasIndex(e => e.UserId);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.User)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
