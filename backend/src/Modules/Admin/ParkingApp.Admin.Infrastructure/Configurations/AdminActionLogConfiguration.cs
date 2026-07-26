using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Admin.Domain.Entities;

namespace ParkingApp.Admin.Infrastructure.Configurations;

public sealed class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    public void Configure(EntityTypeBuilder<AdminActionLog> entity)
    {
        entity.ToTable("AdminActionLogs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).ValueGeneratedNever();

        entity.Property(e => e.ActorEmail).HasMaxLength(256).IsRequired();
        entity.Property(e => e.Action).HasMaxLength(128).IsRequired();
        entity.Property(e => e.ResourceType).HasMaxLength(64).IsRequired();
        entity.Property(e => e.PayloadJson).HasColumnType("text");
        entity.Property(e => e.IpAddress).HasMaxLength(64);
        entity.Property(e => e.UserAgent).HasMaxLength(512);
        entity.Property(e => e.OccurredAtUtc).IsRequired();

        entity.HasIndex(e => e.OccurredAtUtc);
        entity.HasIndex(e => e.ActorUserId);
        entity.HasIndex(e => e.Action);
        entity.HasIndex(e => new { e.ResourceType, e.ResourceId });
    }
}
