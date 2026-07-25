using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CompanyUsageConfiguration : IEntityTypeConfiguration<CompanyUsage>
{
    public void Configure(EntityTypeBuilder<CompanyUsage> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalHoursUsed).HasPrecision(10, 2);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.UsageDate }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.UsageDate });

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Usages)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany()
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);
    }
}

