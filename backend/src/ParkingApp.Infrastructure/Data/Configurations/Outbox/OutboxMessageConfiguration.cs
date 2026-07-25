using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Application.Interfaces;

using ParkingApp.Corporate.Domain;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Infrastructure.Outbox;

namespace ParkingApp.Infrastructure.Data.Configurations.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> entity)
    {
entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TypeName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(256).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasIndex(e => e.IdempotencyKey);
            entity.HasIndex(e => new { e.Status, e.AvailableAfterUtc });
            entity.HasIndex(e => e.CreatedAtUtc);
    }
}
