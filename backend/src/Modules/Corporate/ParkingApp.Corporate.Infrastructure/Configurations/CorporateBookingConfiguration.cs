using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CorporateBookingConfiguration : IEntityTypeConfiguration<CorporateBooking>
{
    public void Configure(EntityTypeBuilder<CorporateBooking> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.VisitorName).HasMaxLength(200);
            entity.Property(e => e.VisitorLicensePlate).HasMaxLength(20);

            // Owned: AccessPolicy (nullable)
            entity.OwnsOne(e => e.AccessPolicy, ap =>
            {
                ap.Property(p => p.AllowedVehiclePlate).HasColumnName("AccessVehiclePlate").HasMaxLength(20);
                ap.Property(p => p.AccessStartUtc).HasColumnName("AccessStartUtc");
                ap.Property(p => p.AccessExpiryUtc).HasColumnName("AccessExpiryUtc");
                ap.Property(p => p.QrCodeToken).HasColumnName("AccessQrToken").HasMaxLength(500);
            });

            entity.HasIndex(e => new { e.CompanyId, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.SlotType });
            entity.HasIndex(e => e.MembershipId);
            entity.HasIndex(e => e.AllocationId);
            entity.HasIndex(e => e.BookingId).IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.CorporateBookings)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany(a => a.CorporateBookings)
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.Booking)
//                 .WithMany()
//                 .HasForeignKey(e => e.BookingId)
//                 .OnDelete(DeleteBehavior.Restrict);
    }
}

