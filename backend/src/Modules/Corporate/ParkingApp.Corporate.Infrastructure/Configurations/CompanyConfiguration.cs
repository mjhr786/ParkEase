using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(64);
            entity.Property(e => e.RegistrationNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BillingAddress).HasMaxLength(500).IsRequired();

            entity.HasIndex(e => e.RegistrationNumber).IsUnique();
            entity.HasIndex(e => e.CreatedByUserId);
            // Unique among non-deleted companies when slug is set
            entity.HasIndex(e => e.Slug)
                .IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL AND \"IsDeleted\" = false")
                .HasDatabaseName("IX_Companies_Slug");

//             entity.HasOne(e => e.CreatedByUser)
//                 .WithMany()
//                 .HasForeignKey(e => e.CreatedByUserId)
//                 .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
    }
}

