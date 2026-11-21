using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> entity)
    {
        entity.ToTable("PaymentWebhookEvents");
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ProviderEventId).HasMaxLength(128).IsRequired();
        entity.Property(e => e.Status).HasConversion<int>();
        entity.Property(e => e.Outcome).HasMaxLength(64);
        entity.Property(e => e.PayloadSha256).HasMaxLength(64);
        entity.Property(e => e.RefundAttemptCount).IsRequired().HasDefaultValue(0);
        entity.Property(e => e.ProviderPaymentId).HasMaxLength(64);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Currency).HasMaxLength(3);
        entity.Property(e => e.CreatedAt).HasColumnName("CreatedAtUtc");
        entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAtUtc");

        entity.HasIndex(e => new { e.Provider, e.ProviderEventId }).IsUnique();
        entity.HasQueryFilter(e => !e.IsDeleted);
    }
}
