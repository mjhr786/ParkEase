using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.Email)
            .HasConversion(
                email => email.Value,
                value => new Email(value))
            .HasMaxLength(255)
            .IsRequired();
        entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
        entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
        entity.Property(e => e.RefreshToken).HasMaxLength(500);
        entity.Property(e => e.SessionChannel).HasConversion<int?>();
        entity.Property(e => e.SessionCompanyRole).HasMaxLength(32);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
