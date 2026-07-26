using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class ParkingSpaceConfiguration : IEntityTypeConfiguration<ParkingSpace>
{
    public void Configure(EntityTypeBuilder<ParkingSpace> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500).IsRequired();
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.State); // Added index for State
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ZoneCode).HasMaxLength(64);
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.DailyRate).HasPrecision(18, 2);
            entity.Property(e => e.WeeklyRate).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.DynamicMinMultiplier).HasPrecision(9, 4);
            entity.Property(e => e.DynamicMaxMultiplier).HasPrecision(9, 4);
            entity.Property(e => e.PeakHourMultiplier).HasPrecision(9, 4);
            entity.Property(e => e.WeekendMultiplier).HasPrecision(9, 4);
            entity.Property(e => e.TimeZoneId).HasMaxLength(64).HasDefaultValue("UTC");
            entity.Property(e => e.EvChargingRatePerHour).HasPrecision(18, 2);
            entity.Property(e => e.EvIdleRatePerHour).HasPrecision(18, 2);
            entity.Property(e => e.EvRatePerKwh).HasPrecision(18, 2);
            entity.Property(e => e.EvPricingMode).HasConversion<int>();
            entity.HasIndex(e => e.HasEvCharging);
            entity.Property(e => e.ListingCategory).HasConversion<int>();
            entity.HasIndex(e => e.ListingCategory);
            entity.HasIndex(e => e.InstantBook);
            entity.HasIndex(e => e.IsBayGuidanceEnabled);
            entity.HasIndex(e => e.IsValetEnabled);
            entity.Property(e => e.DefaultFacilityLevel).HasMaxLength(32);
            entity.Property(e => e.DefaultFacilityZone).HasMaxLength(64);
            entity.Property(e => e.IndoorGuidanceNotes).HasMaxLength(2000);
            entity.Property(e => e.Amenities).HasMaxLength(1000);
            entity.Property(e => e.AllowedVehicleTypes).HasMaxLength(500);
            entity.Property(e => e.ImageUrls).HasMaxLength(4000);
            entity.Property(e => e.SpecialInstructions).HasMaxLength(2000);
            entity.Property(e => e.OwnershipType).HasConversion<int>();
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.ZoneCode);
            entity.HasIndex(e => e.CompanyOwnerId);
            entity.HasIndex(e => e.OwnershipType);
            entity.HasIndex(e => new { e.Latitude, e.Longitude });
            // Marketplace browse/list: public active inventory (excludes corporate-only)
            entity.HasIndex(e => new { e.City, e.CreatedAt })
                .HasDatabaseName("IX_ParkingSpaces_PublicActive")
                .HasFilter("\"IsActive\" = true AND \"IsDeleted\" = false AND \"IsCorporateOnly\" = false");
            
            // PostGIS spatial column configuration
            entity.Property(e => e.Location)
                .HasColumnType("geography (point)");
            entity.HasIndex(e => e.Location)
                .HasMethod("gist"); // GiST index for spatial queries
            
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            // Foreign keys are mapped as standard properties without navigation constraints
            // to maintain modular monolith isolation boundaries.
    }
}
