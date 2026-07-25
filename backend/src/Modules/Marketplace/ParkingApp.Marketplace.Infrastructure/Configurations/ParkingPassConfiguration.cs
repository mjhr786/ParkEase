using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.ValueObjects;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class ParkingPassConfiguration : IEntityTypeConfiguration<ParkingPass>
{
    public void Configure(EntityTypeBuilder<ParkingPass> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.ParkingZoneCode).HasMaxLength(64);
            entity.Property(e => e.CorporateBatchReference).HasMaxLength(100);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.HasIndex(e => new { e.UserId, e.ParkingSpaceId });
            entity.HasIndex(e => new { e.UserId, e.ParkingZoneCode });
            entity.HasIndex(e => e.AllocatedByUserId);
            entity.HasIndex(e => new { e.CreatedAt, e.UserId });
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CoverageType)
                .HasConversion<int>()
                .IsRequired();

            entity.OwnsOne(e => e.PassType, owned =>
            {
                owned.Property(p => p.Kind)
                    .HasColumnName("PassType")
                    .HasConversion<int>()
                    .IsRequired();
            });

            entity.OwnsOne(e => e.Duration, owned =>
            {
                owned.Property(d => d.StartDateUtc)
                    .HasColumnName("StartDateUtc")
                    .HasColumnType("timestamp with time zone")
                    .IsRequired();
                owned.Property(d => d.EndDateUtc)
                    .HasColumnName("EndDateUtc")
                    .HasColumnType("timestamp with time zone")
                    .IsRequired();
            });

            entity.OwnsOne(e => e.UsagePolicy, owned =>
            {
                owned.Property(p => p.Mode)
                    .HasColumnName("UsageMode")
                    .HasConversion<int>()
                    .IsRequired();
                owned.Property(p => p.DailyHourLimit)
                    .HasColumnName("DailyHourLimit");
            });

            entity.Navigation(e => e.PassType).IsRequired();
            entity.Navigation(e => e.Duration).IsRequired();
            entity.Navigation(e => e.UsagePolicy).IsRequired();

            // UserId / AllocatedByUserId are ID-centric; DB FKs remain from migrations.

            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.ParkingPasses)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.SetNull);
    }
}
