using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Shared;

namespace ParkingApp.Domain.Corporate;

/// <summary>
/// Snapshot line on a corporate invoice. Amounts are frozen at generation time.
/// </summary>
public class CorporateInvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public CorporateInvoiceLineType LineType { get; private set; }
    public Guid? AllocationId { get; private set; }
    public Guid? BookingId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal Amount { get; private set; }

    public virtual CorporateInvoice Invoice { get; private set; } = null!;

    private CorporateInvoiceLineItem()
    {
    }

    public static CorporateInvoiceLineItem Create(
        Guid invoiceId,
        CorporateInvoiceLineType lineType,
        string description,
        decimal quantity,
        decimal unitAmount,
        Guid? allocationId = null,
        Guid? bookingId = null)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("Invoice ID is required.", nameof(invoiceId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }

        if (unitAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitAmount), "Unit amount cannot be negative.");
        }

        var amount = Math.Round(quantity * unitAmount, 2, MidpointRounding.AwayFromZero);

        return new CorporateInvoiceLineItem
        {
            InvoiceId = invoiceId,
            LineType = lineType,
            AllocationId = allocationId,
            BookingId = bookingId,
            Description = description.Trim(),
            Quantity = Math.Round(quantity, 4, MidpointRounding.AwayFromZero),
            UnitAmount = Math.Round(unitAmount, 2, MidpointRounding.AwayFromZero),
            Amount = amount
        };
    }
}
