using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.ValueObjects;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
entity.HasKey(e => e.Id);
            // Money VO mapped onto existing Amount + Currency columns (no migration)
            entity.OwnsOne(e => e.Charge, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();
                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });
            entity.Navigation(e => e.Charge).IsRequired();
            entity.Ignore(e => e.Amount);
            entity.Ignore(e => e.Currency);
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.PaymentGatewayReference).HasMaxLength(200);
            entity.Property(e => e.PaymentGateway).HasMaxLength(50);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
            entity.Property(e => e.RefundReason).HasMaxLength(500);
            entity.Property(e => e.RefundTransactionId).HasMaxLength(100);
            entity.Property(e => e.ReceiptUrl).HasMaxLength(500);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.Metadata).HasMaxLength(4000);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.BookingId);
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.Booking)
                .WithOne(b => b.Payment)
                .HasForeignKey<Payment>(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserId is ID-centric; DB FK to Users remains from migrations.
    }
}
