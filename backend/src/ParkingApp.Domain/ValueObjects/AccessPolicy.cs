namespace ParkingApp.Domain.ValueObjects;

/// <summary>
/// Represents an access control policy for visitor parking.
/// Contains vehicle identification, time-limited access window, and QR code token.
/// </summary>
public sealed record AccessPolicy
{
    public string AllowedVehiclePlate { get; private init; } = string.Empty;
    public DateTime AccessStartUtc { get; private init; }
    public DateTime AccessExpiryUtc { get; private init; }
    public string QrCodeToken { get; private init; } = string.Empty;

    private AccessPolicy()
    {
    }

    private AccessPolicy(string allowedVehiclePlate, DateTime accessStartUtc, DateTime accessExpiryUtc, string qrCodeToken)
    {
        if (string.IsNullOrWhiteSpace(allowedVehiclePlate))
        {
            throw new ArgumentException("Vehicle plate is required for access policy.", nameof(allowedVehiclePlate));
        }

        var normalizedStart = Normalize(accessStartUtc);
        var normalizedExpiry = Normalize(accessExpiryUtc);

        if (normalizedExpiry <= normalizedStart)
        {
            throw new ArgumentException("Access expiry must be after access start.");
        }

        if (string.IsNullOrWhiteSpace(qrCodeToken))
        {
            throw new ArgumentException("QR code token is required.", nameof(qrCodeToken));
        }

        AllowedVehiclePlate = allowedVehiclePlate.Trim().ToUpperInvariant();
        AccessStartUtc = normalizedStart;
        AccessExpiryUtc = normalizedExpiry;
        QrCodeToken = qrCodeToken;
    }

    public static AccessPolicy Create(string allowedVehiclePlate, DateTime accessStartUtc, DateTime accessExpiryUtc)
    {
        var token = GenerateQrToken();
        return new AccessPolicy(allowedVehiclePlate, accessStartUtc, accessExpiryUtc, token);
    }

    public static AccessPolicy Create(string allowedVehiclePlate, DateTime accessStartUtc, DateTime accessExpiryUtc, string qrCodeToken)
    {
        return new AccessPolicy(allowedVehiclePlate, accessStartUtc, accessExpiryUtc, qrCodeToken);
    }

    public bool IsActive(DateTime utcNow)
    {
        var now = Normalize(utcNow);
        return now >= AccessStartUtc && now <= AccessExpiryUtc;
    }

    public bool IsExpired(DateTime utcNow)
    {
        return Normalize(utcNow) > AccessExpiryUtc;
    }

    public bool IsVehicleAllowed(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
        {
            return false;
        }

        return string.Equals(AllowedVehiclePlate, plate.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateQrToken()
    {
        return $"VIS-{Guid.NewGuid():N}".ToUpperInvariant();
    }

    private static DateTime Normalize(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
