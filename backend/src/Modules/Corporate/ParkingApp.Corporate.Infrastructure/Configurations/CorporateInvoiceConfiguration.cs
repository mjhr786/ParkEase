using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CorporateInvoiceConfiguration : IEntityTypeConfiguration<CorporateInvoice>
{
    public void Configure(EntityTypeBuilder<CorporateInvoice> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.PaymentReference).HasMaxLength(200);
            entity.Property(e => e.PaymentNotes).HasMaxLength(1000);
            entity.Property(e => e.VoidReason).HasMaxLength(500);

            entity.HasIndex(e => new { e.CompanyId, e.InvoiceNumber }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.Status, e.IssuedAt });
            entity.HasIndex(e => new { e.CompanyId, e.PeriodStart, e.PeriodEnd });

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.LineItems)
                .WithOne(l => l.Invoice)
                .HasForeignKey(l => l.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
    }
}

