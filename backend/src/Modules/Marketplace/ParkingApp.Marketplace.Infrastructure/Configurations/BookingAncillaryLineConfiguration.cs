using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Configurations;

internal sealed class BookingAncillaryLineConfiguration : IEntityTypeConfiguration<BookingAncillaryLine>
{
    public void Configure(EntityTypeBuilder<BookingAncillaryLine> entity)
    {
        entity.ToTable("BookingAncillaryLines");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SnapshotName).HasMaxLength(120).IsRequired();
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Ignore(e => e.LineTotal);
        entity.HasIndex(e => e.BookingId);
        entity.HasIndex(e => e.ServiceId);
        entity.HasQueryFilter(e => !e.IsDeleted);

        entity.HasOne(e => e.Booking)
            .WithMany(b => b.AncillaryLines)
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
