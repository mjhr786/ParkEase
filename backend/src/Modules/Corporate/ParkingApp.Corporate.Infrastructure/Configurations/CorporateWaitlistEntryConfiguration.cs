using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CorporateWaitlistEntryConfiguration : IEntityTypeConfiguration<CorporateWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<CorporateWaitlistEntry> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.VehicleNumber).HasMaxLength(20);
            entity.Property(e => e.VisitorName).HasMaxLength(200);
            entity.Property(e => e.VisitorLicensePlate).HasMaxLength(20);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.Status, e.PriorityAtRequest, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.RequestedStartDateTime, e.RequestedEndDateTime });

            entity.HasOne(e => e.Company)
                .WithMany(c => c.WaitlistEntries)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany()
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.PromotedBooking)
//                 .WithMany()
//                 .HasForeignKey(e => e.PromotedBookingId)
//                 .OnDelete(DeleteBehavior.Restrict);
    }
}

