using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.BookingReference).HasMaxLength(50);
            entity.Property(e => e.QRCode).HasMaxLength(2000);
            entity.Property(e => e.VehicleNumber).HasMaxLength(20);
            entity.Property(e => e.VehicleModel).HasMaxLength(100);
            entity.Property(e => e.VehicleColor).HasMaxLength(50);
            entity.Property(e => e.DiscountCode).HasMaxLength(50);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.BaseAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.ServiceFee).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
            entity.Property(e => e.OverstayFeeAmount).HasPrecision(18, 2);
            entity.Property(e => e.OverstayFeePaidAmount).HasPrecision(18, 2);
            entity.Property(e => e.EvChargingFeeAmount).HasPrecision(18, 2);
            entity.Property(e => e.EvIdleFeeAmount).HasPrecision(18, 2);
            entity.Property(e => e.OverstayFeeTransactionId).HasMaxLength(100);
            entity.Property(e => e.FacilityLevel).HasMaxLength(32);
            entity.Property(e => e.FacilityZone).HasMaxLength(64);
            entity.Property(e => e.BayLabel).HasMaxLength(32);
            entity.Property(e => e.ValetStatus).HasConversion<int>();
            entity.Property(e => e.ValetNotes).HasMaxLength(500);
            // KD-19: corporate-staged bookings excluded from consumer lists via this Marketplace-owned flag
            entity.Property(e => e.IsCorporateStaged).HasDefaultValue(false);
            entity.HasIndex(e => e.IsCorporateStaged);
            entity.HasIndex(e => e.ValetStatus);
            entity.HasIndex(e => e.BookingReference).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ParkingSpaceId);
            entity.HasIndex(e => e.ParkingPassId);
            entity.HasIndex(e => e.EventParkingPackageId);
            entity.HasIndex(e => new { e.StartDateTime, e.EndDateTime });
            // Overlap / capacity: active bookings by space and time window
            // Status: Cancelled=4, Expired=5, Rejected=7
            entity.HasIndex(e => new { e.ParkingSpaceId, e.StartDateTime, e.EndDateTime })
                .HasDatabaseName("IX_Bookings_Space_ActiveWindow")
                .HasFilter("\"IsDeleted\" = false AND \"Status\" NOT IN (4, 5, 7)");
            // Vendor inbox: pending initial + extension requests
            // Status: Pending=0, PendingExtension=8
            entity.HasIndex(e => new { e.ParkingSpaceId, e.CreatedAt })
                .HasDatabaseName("IX_Bookings_Pending_Space")
                .HasFilter("\"IsDeleted\" = false AND \"Status\" IN (0, 8)");
            entity.HasQueryFilter(e => !e.IsDeleted);

            // UserId is ID-centric; DB FK to Users remains from migrations (no Identity.Domain compile ref).
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Bookings)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingPass)
                .WithMany(p => p.Bookings)
                .HasForeignKey(e => e.ParkingPassId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Navigation(e => e.AncillaryLines)
                .HasField("_ancillaryLines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
