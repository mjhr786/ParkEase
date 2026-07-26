using System.Security.Cryptography;
using System.Text;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Facility-scoped IoT camera API key. Secret is stored hashed; plaintext only available at creation.
/// </summary>
public class LprCameraKey : BaseEntity
{
    public Guid ParkingSpaceId { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string KeyId { get; internal set; } = string.Empty;
    public string SecretHash { get; internal set; } = string.Empty;
    public string SecretPrefix { get; internal set; } = string.Empty;
    public bool IsEnabled { get; internal set; } = true;
    public Guid CreatedByUserId { get; internal set; }

    internal LprCameraKey()
    {
    }

    /// <summary>
    /// Creates a camera key and returns the one-time plaintext secret (not stored).
    /// </summary>
    public static (LprCameraKey Key, string PlaintextSecret) Create(
        Guid parkingSpaceId,
        string name,
        Guid createdByUserId,
        string? keyId = null)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (createdByUserId == Guid.Empty)
            throw new ValidationException("createdByUserId", "Creator is required");
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Name is required");

        var plaintext = GenerateSecret();
        var resolvedKeyId = string.IsNullOrWhiteSpace(keyId)
            ? $"cam-{Guid.NewGuid().ToString("N")[..8]}"
            : keyId.Trim();

        if (resolvedKeyId.Length > 64)
            throw new ValidationException("keyId", "Key id must not exceed 64 characters");

        var entity = new LprCameraKey
        {
            ParkingSpaceId = parkingSpaceId,
            Name = name.Trim(),
            KeyId = resolvedKeyId,
            SecretHash = HashSecret(plaintext),
            SecretPrefix = plaintext.Length >= 8 ? plaintext[..8] : plaintext,
            IsEnabled = true,
            CreatedByUserId = createdByUserId
        };

        return (entity, plaintext);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Name is required");
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled) return;
        IsEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable() => SetEnabled(false);

    public static string HashSecret(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ValidationException("secret", "Secret is required");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes);
    }

    public static bool SecretsMatch(string plaintext, string storedHash)
    {
        if (string.IsNullOrEmpty(plaintext) || string.IsNullOrEmpty(storedHash))
            return false;

        var computed = HashSecret(plaintext);
        var a = Encoding.UTF8.GetBytes(computed);
        var b = Encoding.UTF8.GetBytes(storedHash.ToUpperInvariant());
        if (a.Length != b.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "pk_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
