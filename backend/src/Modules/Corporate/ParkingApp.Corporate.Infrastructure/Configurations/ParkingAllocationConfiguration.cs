using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class ParkingAllocationConfiguration : IEntityTypeConfiguration<ParkingAllocation>
{
    public void Configure(EntityTypeBuilder<ParkingAllocation> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            // Defaults live in the domain; avoid ValueGeneratedOnAdd (HasDefaultValue implies it),
            // which confuses change tracking for client-set values on insert.
            entity.Property(e => e.SourceType).HasConversion<int>();
            entity.Property(e => e.LeaseReference).HasMaxLength(100);

            // Owned: Quota
            entity.OwnsOne(e => e.Quota, q =>
            {
                q.Property(p => p.TotalSlots).HasColumnName("TotalSlots").IsRequired();
                q.Property(p => p.FixedSlots).HasColumnName("FixedSlots").IsRequired();
                q.Property(p => p.SharedSlots).HasColumnName("SharedSlots").IsRequired();
            });

            // Owned: BookingPolicy
            entity.OwnsOne(e => e.BookingPolicy, bp =>
            {
                bp.Property(p => p.MaxBookingsPerEmployeePerDay).HasColumnName("MaxBookingsPerDay");
                bp.Property(p => p.MaxBookingsPerEmployeePerWeek).HasColumnName("MaxBookingsPerWeek");
                bp.Property(p => p.PriorityThreshold).HasColumnName("PriorityThreshold");
                bp.Property(p => p.AllowedStartTime).HasColumnName("AllowedStartTime");
                bp.Property(p => p.AllowedEndTime).HasColumnName("AllowedEndTime");
                bp.Property(p => p.AllowWeekends).HasColumnName("AllowWeekends");
            });

            entity.Property(e => e.RejectionReason).HasMaxLength(500);

            entity.HasIndex(e => new { e.CompanyId, e.ParkingSpaceId });
            entity.HasIndex(e => new { e.CompanyId, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.SourceType, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.Status, e.CreatedAt });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.VendorId);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Allocations)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.ParkingSpace)
//                 .WithMany()
//                 .HasForeignKey(e => e.ParkingSpaceId)
//                 .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.ApprovedByUser)
//                 .WithMany()
//                 .HasForeignKey(e => e.ApprovedByUserId)
//                 .OnDelete(DeleteBehavior.Restrict);
    }
}

