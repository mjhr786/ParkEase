using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Services;

/// <summary>
/// Pure calculation of corporate invoice line drafts from billing inputs.
/// </summary>
public interface ICorporateInvoiceCalculator
{
    IReadOnlyList<CorporateInvoiceLineDraft> BuildLines(
        BillingType billingType,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceAllocationChargeInput> allocations,
        IReadOnlyList<InvoiceBookingChargeInput> bookings);
}

public sealed record InvoiceAllocationChargeInput(
    Guid AllocationId,
    string ParkingSpaceTitle,
    string? LeaseReference,
    ParkingAllocationSource SourceType,
    AllocationStatus Status,
    decimal MonthlyRate,
    DateOnly ContractStart,
    DateOnly ContractEnd);

public sealed record InvoiceBookingChargeInput(
    Guid BookingId,
    Guid AllocationId,
    decimal TotalAmount,
    DateTime StartDateTime,
    DateTime EndDateTime,
    BookingStatus Status,
    bool IsVisitorBooking,
    string? MemberName,
    string? VisitorName,
    string? ParkingSpaceTitle);

public sealed class CorporateInvoiceCalculator : ICorporateInvoiceCalculator
{
    public IReadOnlyList<CorporateInvoiceLineDraft> BuildLines(
        BillingType billingType,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceAllocationChargeInput> allocations,
        IReadOnlyList<InvoiceBookingChargeInput> bookings)
    {
        CorporateInvoice.ValidatePeriod(periodStart, periodEnd);

        return billingType switch
        {
            BillingType.ReservedSlots => BuildReservedCapacityLines(periodStart, periodEnd, allocations),
            BillingType.UsageBased => BuildUsageLines(periodStart, periodEnd, bookings),
            _ => throw new ArgumentOutOfRangeException(nameof(billingType), billingType, "Unsupported billing type.")
        };
    }

    private static List<CorporateInvoiceLineDraft> BuildReservedCapacityLines(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceAllocationChargeInput> allocations)
    {
        var fullPeriodDays = periodEnd.DayNumber - periodStart.DayNumber + 1;
        var lines = new List<CorporateInvoiceLineDraft>();

        foreach (var allocation in allocations)
        {
            if (allocation.SourceType != ParkingAllocationSource.VendorLease)
            {
                continue;
            }

            // Pending/Rejected never bill. Active and Expired contracts that overlapped the period can bill.
            if (allocation.Status is not (AllocationStatus.Active or AllocationStatus.Expired))
            {
                continue;
            }

            if (allocation.MonthlyRate <= 0)
            {
                continue;
            }

            var overlapStart = allocation.ContractStart > periodStart ? allocation.ContractStart : periodStart;
            var overlapEnd = allocation.ContractEnd < periodEnd ? allocation.ContractEnd : periodEnd;
            if (overlapEnd < overlapStart)
            {
                continue;
            }

            var overlapDays = overlapEnd.DayNumber - overlapStart.DayNumber + 1;
            var quantity = Math.Round((decimal)overlapDays / fullPeriodDays, 4, MidpointRounding.AwayFromZero);
            var title = string.IsNullOrWhiteSpace(allocation.ParkingSpaceTitle)
                ? "Parking allocation"
                : allocation.ParkingSpaceTitle.Trim();
            var leaseBit = string.IsNullOrWhiteSpace(allocation.LeaseReference)
                ? string.Empty
                : $" ({allocation.LeaseReference.Trim()})";
            var description = $"Reserved capacity — {title}{leaseBit} · {overlapDays}d";

            lines.Add(new CorporateInvoiceLineDraft(
                CorporateInvoiceLineType.ReservedCapacity,
                description,
                quantity,
                allocation.MonthlyRate,
                allocation.AllocationId));
        }

        return lines
            .OrderBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CorporateInvoiceLineDraft> BuildUsageLines(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceBookingChargeInput> bookings)
    {
        var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndExclusiveUtc = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var lines = new List<CorporateInvoiceLineDraft>();

        foreach (var booking in bookings)
        {
            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Rejected)
            {
                continue;
            }

            if (booking.TotalAmount <= 0)
            {
                continue;
            }

            // Bill by booking start date in period (avoids double-billing multi-day windows).
            if (booking.StartDateTime < periodStartUtc || booking.StartDateTime >= periodEndExclusiveUtc)
            {
                continue;
            }

            var who = booking.IsVisitorBooking
                ? (string.IsNullOrWhiteSpace(booking.VisitorName) ? "Visitor" : booking.VisitorName.Trim())
                : (string.IsNullOrWhiteSpace(booking.MemberName) ? "Employee" : booking.MemberName.Trim());
            var space = string.IsNullOrWhiteSpace(booking.ParkingSpaceTitle)
                ? "Parking"
                : booking.ParkingSpaceTitle.Trim();
            var description =
                $"Usage — {who} · {space} · {booking.StartDateTime:yyyy-MM-dd}";

            lines.Add(new CorporateInvoiceLineDraft(
                CorporateInvoiceLineType.Usage,
                description,
                Quantity: 1m,
                UnitAmount: booking.TotalAmount,
                AllocationId: booking.AllocationId,
                BookingId: booking.BookingId));
        }

        if (lines.Count > CorporateInvoice.MaxLineItems)
        {
            throw new InvalidOperationException(
                $"Invoice cannot exceed {CorporateInvoice.MaxLineItems} line items. Narrow the billing period and try again.");
        }

        return lines
            .OrderBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
