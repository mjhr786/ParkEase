using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CorporateSsoAuditEventConfiguration : IEntityTypeConfiguration<CorporateSsoAuditEvent>
{
    public void Configure(EntityTypeBuilder<CorporateSsoAuditEvent> entity)
    {
        entity.ToTable("CorporateSsoAuditEvents");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Action).HasMaxLength(64).IsRequired();
        entity.Property(e => e.Outcome).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ErrorCode).HasMaxLength(64);
        entity.Property(e => e.IpAddress).HasMaxLength(64);
        entity.Property(e => e.UserAgent).HasMaxLength(512);
        entity.Property(e => e.DetailJson).HasColumnType("text");

        entity.HasIndex(e => e.CompanyId);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => new { e.CompanyId, e.CreatedAt });

        // Audit is append-only; soft-delete filter still applied for BaseEntity consistency.
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
