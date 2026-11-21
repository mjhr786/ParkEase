using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Services;

internal static class PaymentChargeResolver
{
    internal const string PurposeBooking = "booking";
    internal const string PurposeExtension = "extension";
    internal const string PurposeOverstay = "overstay";

    internal sealed record Charge(decimal Amount, string Purpose, Dictionary<string, string> Notes);

    public static bool TryResolve(
        Booking booking,
        bool? payOverstayFee,
        out Charge charge,
        out string? error)
    {
        charge = null!;
        error = null;

        var payOverstay = payOverstayFee == true
            || (payOverstayFee != false
                && booking.OverstayFeeOutstanding > 0
                && booking.Status is BookingStatus.InProgress or BookingStatus.Completed or BookingStatus.Confirmed);

        if (payOverstay)
        {
            if (booking.OverstayFeeOutstanding <= 0)
            {
                error = "No outstanding overstay fee";
                return false;
            }

            charge = new Charge(booking.OverstayFeeOutstanding, PurposeOverstay, OverstayNotes(booking));
            return true;
        }

        if (booking.Status != BookingStatus.AwaitingPayment &&
            booking.Status != BookingStatus.AwaitingExtensionPayment)
        {
            error = "Booking is not awaiting payment";
            return false;
        }

        var isExtension = booking.Status == BookingStatus.AwaitingExtensionPayment;
        var amount = isExtension
            ? (booking.PendingExtensionAmount ?? booking.TotalAmount)
            : booking.TotalAmount;
        var purpose = isExtension ? PurposeExtension : PurposeBooking;
        charge = new Charge(amount, purpose, BookingOrExtensionNotes(booking, purpose));
        return true;
    }

    public static Charge ResolveExpectedCapture(Booking booking)
    {
        var isExtensionPayment = booking.Status == BookingStatus.AwaitingExtensionPayment;
        var isOverstayPayment = !isExtensionPayment
            && booking.Status is not BookingStatus.AwaitingPayment
            && booking.OverstayFeeOutstanding > 0;

        if (isOverstayPayment)
            return new Charge(booking.OverstayFeeOutstanding, PurposeOverstay, OverstayNotes(booking));

        if (isExtensionPayment)
            return new Charge(
                booking.PendingExtensionAmount ?? booking.TotalAmount,
                PurposeExtension,
                BookingOrExtensionNotes(booking, PurposeExtension));

        return new Charge(
            booking.TotalAmount,
            PurposeBooking,
            BookingOrExtensionNotes(booking, PurposeBooking));
    }

    private static Dictionary<string, string> OverstayNotes(Booking booking) => new()
    {
        { "bookingId", booking.Id.ToString() },
        { "purpose", PurposeOverstay },
        { "bookingReference", booking.BookingReference ?? string.Empty }
    };

    private static Dictionary<string, string> BookingOrExtensionNotes(Booking booking, string purpose) => new()
    {
        { "bookingId", booking.Id.ToString() },
        { "purpose", purpose }
    };
}
