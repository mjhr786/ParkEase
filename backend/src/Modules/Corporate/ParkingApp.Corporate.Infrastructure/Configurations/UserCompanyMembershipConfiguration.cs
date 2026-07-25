using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class UserCompanyMembershipConfiguration : IEntityTypeConfiguration<UserCompanyMembership>
{
    public void Configure(EntityTypeBuilder<UserCompanyMembership> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeCode).HasMaxLength(50);
            entity.Property(e => e.Priority).HasDefaultValue(1);

            entity.HasIndex(e => new { e.CompanyId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.IsActive });
            entity.HasIndex(e => new { e.CompanyId, e.Role, e.IsActive });
            entity.HasIndex(e => new { e.CompanyId, e.Role, e.CreatedAt });
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Memberships)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

//             entity.HasOne(e => e.User)
//                 .WithMany()
//                 .HasForeignKey(e => e.UserId)
//                 .OnDelete(DeleteBehavior.Restrict);
    }
}

