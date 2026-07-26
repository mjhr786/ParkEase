using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class LprPlateRuleConfiguration : IEntityTypeConfiguration<LprPlateRule>
{
    public void Configure(EntityTypeBuilder<LprPlateRule> entity)
    {
        entity.ToTable("LprPlateRules");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.LicensePlateNormalized).HasMaxLength(20).IsRequired();
        entity.Property(e => e.Note).HasMaxLength(200);

        entity.HasIndex(e => new { e.ParkingSpaceId, e.LicensePlateNormalized, e.RuleType })
            .IsUnique()
            .HasDatabaseName("IX_LprPlateRules_Space_Plate_Type")
            .HasFilter("\"IsDeleted\" = false");

        entity.HasIndex(e => e.ParkingSpaceId).HasDatabaseName("IX_LprPlateRules_ParkingSpaceId");

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
