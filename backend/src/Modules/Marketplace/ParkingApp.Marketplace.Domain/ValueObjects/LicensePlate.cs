using System.Text.RegularExpressions;

namespace ParkingApp.Marketplace.Domain.ValueObjects;

/// <summary>
/// License plate normalization and fuzzy matching for LPR.
/// </summary>
public static partial class LicensePlate
{
    public const int MaxLength = 20;

    /// <summary>
    /// Trims, uppercases, and removes internal whitespace. Keeps hyphens/punctuation.
    /// Returns null if empty after normalize.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var chars = value.Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray();
        if (chars.Length == 0)
            return null;

        var normalized = new string(chars);
        return normalized.Length > MaxLength ? normalized[..MaxLength] : normalized;
    }

    /// <summary>
    /// Alphanumeric-only match key for fuzzy compare (e.g. KA-01 AB → KA01AB).
    /// </summary>
    public static string? ToMatchKey(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
            return null;

        var key = NonAlphanumeric().Replace(normalized, string.Empty);
        return string.IsNullOrEmpty(key) ? null : key;
    }

    /// <summary>
    /// True if plates match exactly after normalize, or after fuzzy match-key comparison.
    /// </summary>
    public static bool Matches(string? a, string? b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        if (na is null || nb is null)
            return false;

        if (string.Equals(na, nb, StringComparison.Ordinal))
            return true;

        var ka = ToMatchKey(na);
        var kb = ToMatchKey(nb);
        return ka is not null
               && kb is not null
               && string.Equals(ka, kb, StringComparison.Ordinal);
    }

    public static bool IsValid(string? value) => !string.IsNullOrEmpty(Normalize(value));

    [GeneratedRegex("[^A-Z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumeric();
}
