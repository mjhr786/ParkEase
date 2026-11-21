using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal static class PaymentCaptureMatcher
{
    public static long ToMinorUnits(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static bool Matches(
        long amountMinor,
        string? currency,
        IReadOnlyDictionary<string, string>? metadata,
        PaymentCaptureCheck expected)
    {
        if (expected.BookingId == Guid.Empty)
            return false;
        if (expected.Amount <= 0)
            return false;
        if (string.IsNullOrWhiteSpace(expected.Currency))
            return false;
        if (ToMinorUnits(expected.Amount) != amountMinor)
            return false;
        if (string.IsNullOrWhiteSpace(currency)
            || !string.Equals(currency, expected.Currency, StringComparison.OrdinalIgnoreCase))
            return false;
        if (metadata is null)
            return false;
        if (!metadata.TryGetValue("bookingId", out var bookingIdValue)
            || !Guid.TryParse(bookingIdValue, out var bookingId)
            || bookingId != expected.BookingId)
            return false;
        if (!metadata.TryGetValue("purpose", out var purpose)
            || !string.Equals(purpose, expected.Purpose, StringComparison.Ordinal))
            return false;

        return true;
    }
}
