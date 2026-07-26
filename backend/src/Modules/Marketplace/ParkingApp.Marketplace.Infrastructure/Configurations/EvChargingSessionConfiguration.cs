using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class EvChargingSessionConfiguration : IEntityTypeConfiguration<EvChargingSession>
{
    public void Configure(EntityTypeBuilder<EvChargingSession> entity)
    {
        entity.ToTable("EvChargingSessions");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.StationId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.OcppTransactionId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Source).HasMaxLength(32).IsRequired();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.MeterStartKwh).HasPrecision(18, 3);
        entity.Property(e => e.LastMeterKwh).HasPrecision(18, 3);
        entity.Property(e => e.MeterEndKwh).HasPrecision(18, 3);
        entity.Property(e => e.EnergyDeliveredKwh).HasPrecision(18, 3);
        entity.Property(e => e.RatePerKwh).HasPrecision(18, 2);
        entity.Property(e => e.EnergyFeeAmount).HasPrecision(18, 2);

        entity.HasIndex(e => e.OcppTransactionId)
            .IsUnique()
            .HasDatabaseName("IX_EvChargingSessions_OcppTransactionId");
        entity.HasIndex(e => new { e.BookingId, e.StartedAtUtc })
            .HasDatabaseName("IX_EvChargingSessions_Booking_Started");
        entity.HasIndex(e => e.ParkingSpaceId)
            .HasDatabaseName("IX_EvChargingSessions_ParkingSpaceId");

        entity.HasOne(e => e.Booking)
            .WithMany()
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
