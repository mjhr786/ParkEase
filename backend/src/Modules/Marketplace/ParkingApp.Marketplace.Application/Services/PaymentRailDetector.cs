using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>KD-PAY-22 rail detection for verify. Hint never overrides PaymentOrder.Rail or id prefix.</summary>
internal static class PaymentRailDetector
{
    public const string Stripe = "stripe";
    public const string Upi = "upi";

    public sealed record Decision(bool Mismatch, string? Rail);

    public static Decision Detect(
        PaymentOrder? orderById,
        string? paymentId,
        string? orderId,
        string? signature,
        string? railHint)
    {
        var fromOrder = NormalizeRail(orderById?.Rail);
        var fromPrefix = PrefixRail(paymentId, orderId);
        var fromHint = HintRail(railHint, signature);

        if (fromHint is not null)
        {
            if (fromOrder is not null && !Same(fromHint, fromOrder))
                return new Decision(true, fromOrder);
            if (fromPrefix is not null && !Same(fromHint, fromPrefix))
                return new Decision(true, fromPrefix);
        }

        return new Decision(false, fromOrder ?? fromPrefix ?? fromHint);
    }

    internal static string? PrefixRail(string? paymentId, string? orderId)
    {
        if (Starts(paymentId, "pi_"))
            return Stripe;
        if (Starts(paymentId, "pay_"))
            return Upi;
        if (Starts(orderId, "pi_"))
            return Stripe;
        if (Starts(orderId, "order_"))
            return Upi;
        return null;
    }

    private static string? HintRail(string? railHint, string? signature)
    {
        var normalized = NormalizeRail(railHint);
        if (normalized is not null)
            return normalized;
        if (string.Equals(signature, "stripe", StringComparison.OrdinalIgnoreCase))
            return Stripe;
        return null;
    }

    internal static string? NormalizeRail(string? rail)
    {
        if (string.IsNullOrWhiteSpace(rail))
            return null;
        if (string.Equals(rail, Stripe, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rail, PaymentOrder.RailStripe, StringComparison.OrdinalIgnoreCase))
            return Stripe;
        if (string.Equals(rail, Upi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rail, PaymentOrder.RailUpi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rail, "razorpay", StringComparison.OrdinalIgnoreCase))
            return Upi;
        return null;
    }

    private static bool Starts(string? value, string prefix) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
