using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> entity)
    {
        entity.ToTable("PaymentOrders");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Purpose).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Rail).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ProviderOrderId).HasMaxLength(64).IsRequired();
        entity.Property(e => e.ProviderPaymentId).HasMaxLength(64);
        entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
        entity.Property(e => e.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .HasColumnType("character(3)")
            .IsRequired();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.MetadataJson).HasMaxLength(2000);
        entity.Property(e => e.CreatedAt).HasColumnName("CreatedAtUtc");
        entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAtUtc");

        entity.HasIndex(e => new { e.Provider, e.ProviderOrderId }).IsUnique();
        entity.HasIndex(e => new { e.Provider, e.ProviderPaymentId })
            .IsUnique()
            .HasFilter("\"ProviderPaymentId\" IS NOT NULL");
        entity.HasIndex(e => new { e.BookingId, e.Purpose })
            .IsUnique()
            .HasFilter($"\"IsDeleted\" = FALSE AND \"Status\" IN ({(int)PaymentOrderStatus.Created}, {(int)PaymentOrderStatus.Pending})");
        entity.HasIndex(e => new { e.Status, e.ExpiresAtUtc });
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.Booking)
            .WithMany()
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
