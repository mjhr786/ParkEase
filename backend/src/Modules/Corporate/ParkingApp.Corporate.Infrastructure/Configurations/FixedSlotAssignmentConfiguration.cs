using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class FixedSlotAssignmentConfiguration : IEntityTypeConfiguration<FixedSlotAssignment>
{
    public void Configure(EntityTypeBuilder<FixedSlotAssignment> entity)
    {
entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.SlotNumber }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId });

            entity.HasOne(e => e.Allocation)
                .WithMany(a => a.FixedAssignments)
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);
    }
}

