namespace ParkingApp.Marketplace.Application.Services;

internal static class IndianMobileNormalizer
{
    public static bool TryNormalize(string? phone, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.Length == 11 && digits[0] == '0')
            digits = digits[1..];

        if (digits.Length == 10 && digits[0] is >= '6' and <= '9')
        {
            normalized = digits;
            return true;
        }

        return false;
    }
}
