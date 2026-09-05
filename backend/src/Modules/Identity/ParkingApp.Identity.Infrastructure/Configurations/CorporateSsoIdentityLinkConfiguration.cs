using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Infrastructure.Configurations;

internal sealed class CorporateSsoIdentityLinkConfiguration : IEntityTypeConfiguration<CorporateSsoIdentityLink>
{
    public void Configure(EntityTypeBuilder<CorporateSsoIdentityLink> entity)
    {
        entity.ToTable("CorporateSsoIdentityLinks");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Protocol).HasConversion<short>().IsRequired();
        entity.Property(e => e.Subject).HasMaxLength(512).IsRequired();
        entity.Property(e => e.IdPIssuer).HasMaxLength(512).IsRequired();
        entity.Property(e => e.ProviderEmail).HasMaxLength(320);
        entity.Property(e => e.LinkedAtUtc).IsRequired();
        entity.Property(e => e.IsDisabled).HasDefaultValue(false);

        // Unique subject per company + protocol
        entity.HasIndex(e => new { e.CompanyId, e.Protocol, e.Subject }).IsUnique();
        // One link per user per company (MVP)
        entity.HasIndex(e => new { e.CompanyId, e.UserId }).IsUnique();
        entity.HasIndex(e => e.UserId);

        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.User)
            .WithMany(u => u.CorporateSsoLinks)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
