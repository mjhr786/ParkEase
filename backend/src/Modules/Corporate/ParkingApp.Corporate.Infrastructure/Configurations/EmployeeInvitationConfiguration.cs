using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class EmployeeInvitationConfiguration : IEntityTypeConfiguration<EmployeeInvitation>
{
    public void Configure(EntityTypeBuilder<EmployeeInvitation> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.InvitationToken).HasMaxLength(500).IsRequired();

            entity.HasIndex(e => new { e.CompanyId, e.Email });
            entity.HasIndex(e => e.InvitationToken).IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Invitations)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.InvitedByUser)
//                 .WithMany()
//                 .HasForeignKey(e => e.InvitedByUserId)
//                 .OnDelete(DeleteBehavior.Restrict);
    }
}

