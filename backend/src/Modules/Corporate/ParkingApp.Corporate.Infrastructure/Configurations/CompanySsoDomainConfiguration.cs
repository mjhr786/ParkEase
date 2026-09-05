using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CompanySsoDomainConfiguration : IEntityTypeConfiguration<CompanySsoDomain>
{
    public void Configure(EntityTypeBuilder<CompanySsoDomain> entity)
    {
        entity.ToTable("CompanySsoDomains");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Domain).HasMaxLength(255).IsRequired();
        entity.Property(e => e.VerificationToken).HasMaxLength(128).IsRequired();
        entity.Property(e => e.IsVerified).HasDefaultValue(false);

        entity.HasIndex(e => new { e.CompanyId, e.Domain }).IsUnique();
        // Partial unique: verified domain claimed by at most one company (Postgres)
        entity.HasIndex(e => e.Domain)
            .IsUnique()
            .HasFilter("\"IsVerified\" = true AND \"IsDeleted\" = false")
            .HasDatabaseName("IX_CompanySsoDomains_Domain_Verified");

        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
