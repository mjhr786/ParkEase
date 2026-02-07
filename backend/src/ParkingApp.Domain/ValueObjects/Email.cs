using System.Text.RegularExpressions;

namespace ParkingApp.Domain.ValueObjects;

public record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; init; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();
        
        if (!EmailRegex.IsMatch(normalized))
            throw new ArgumentException("Invalid email format", nameof(value));

        Value = normalized;
    }

    public static implicit operator string(Email email) => email.Value;
    public static explicit operator Email(string value) => new(value);

    public override string ToString() => Value;
}
