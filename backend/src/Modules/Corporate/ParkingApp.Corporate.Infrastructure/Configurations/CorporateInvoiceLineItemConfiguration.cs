using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.Infrastructure.Configurations;

internal sealed class CorporateInvoiceLineItemConfiguration : IEntityTypeConfiguration<CorporateInvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<CorporateInvoiceLineItem> entity)
    {
entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitAmount).HasPrecision(18, 2);
            entity.Property(e => e.Amount).HasPrecision(18, 2);

            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.AllocationId);
            entity.HasIndex(e => e.BookingId);
    }
}

