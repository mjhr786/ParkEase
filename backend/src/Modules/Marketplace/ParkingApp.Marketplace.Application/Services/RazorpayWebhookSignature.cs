using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ParkingApp.Marketplace.Application.Services;

public static class RazorpayWebhookSignature
{
    public const int MaxBodyBytes = 256 * 1024;
    public const string SignatureHeaderName = "X-Razorpay-Signature";
    public const string EventIdHeaderName = "X-Razorpay-Event-Id";
    public const string RetryAfterSeconds = "15";

    public static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            return false;
        if (!string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrEmpty(parsed.CharSet))
            return true;
        return string.Equals(parsed.CharSet, "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryNormalizeSignature(string? header, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(header))
            return false;

        normalized = header.Trim().ToLowerInvariant();
        if (normalized.Length == 0 || (normalized.Length & 1) == 1)
            return false;

        foreach (var c in normalized)
        {
            if (c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    public static string ComputeHex(ReadOnlySpan<byte> body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret ?? string.Empty);
        var hash = HMACSHA256.HashData(key, body.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool FixedTimeEqualsHex(string computedLower, string headerLower)
    {
        if (computedLower.Length != headerLower.Length)
            return false;

        try
        {
            var left = Convert.FromHexString(computedLower);
            var right = Convert.FromHexString(headerLower);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string PayloadSha256Hex(ReadOnlySpan<byte> body) =>
        Convert.ToHexString(SHA256.HashData(body.ToArray())).ToLowerInvariant();

    public static string FallbackEventId(string provider, string eventType, string paymentId)
    {
        var material = $"{provider}:{eventType}:{paymentId}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        var id = "rzp_fb_" + hash;
        return id.Length <= 128 ? id : id[..128];
    }
}
