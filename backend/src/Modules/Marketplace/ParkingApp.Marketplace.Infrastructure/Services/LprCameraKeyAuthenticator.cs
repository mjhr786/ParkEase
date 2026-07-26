using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>
/// Authenticates IoT API keys: facility DB registry first, then appsettings fallback.
/// </summary>
public sealed class LprCameraKeyAuthenticator : ILprCameraKeyAuthenticator
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly LprConfigApiKeyOptions _configKeys;

    public LprCameraKeyAuthenticator(
        IMarketplaceUnitOfWork unitOfWork,
        IOptions<LprConfigApiKeyOptions> configKeys)
    {
        _unitOfWork = unitOfWork;
        _configKeys = configKeys.Value;
    }

    public async Task<LprApiKeyAuthResult?> AuthenticateAsync(string secret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        var trimmed = secret.Trim();

        // 1) Facility-scoped DB camera keys
        var hash = LprCameraKey.HashSecret(trimmed);
        var dbKey = await _unitOfWork.LprCameraKeys.FindEnabledBySecretHashAsync(hash, cancellationToken);
        if (dbKey is not null)
        {
            return new LprApiKeyAuthResult(
                dbKey.KeyId,
                new[] { dbKey.ParkingSpaceId },
                FromDatabase: true);
        }

        // 2) Config fallback (dev / ops bootstrap) — optional unrestricted or listed facilities
        var match = _configKeys.ApiKeys?
            .Where(k => k.IsEnabled
                        && !string.IsNullOrWhiteSpace(k.Secret)
                        && !IsPlaceholder(k.Secret)
                        && SecretsEqual(k.Secret, trimmed))
            .FirstOrDefault();

        if (match is null)
            return null;

        var allowed = (match.AllowedParkingSpaceIds ?? new List<string>())
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        return new LprApiKeyAuthResult(
            match.KeyId,
            allowed,
            FromDatabase: false);
    }

    private static bool IsPlaceholder(string secret) =>
        secret.Contains("SET_VIA_USER_SECRETS", StringComparison.OrdinalIgnoreCase)
        || secret.Equals("change-me", StringComparison.OrdinalIgnoreCase);

    private static bool SecretsEqual(string expected, string actual)
    {
        // Config secrets compared via same hash path as DB for consistent timing shape
        try
        {
            return LprCameraKey.SecretsMatch(actual, LprCameraKey.HashSecret(expected));
        }
        catch
        {
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }
    }
}

/// <summary>
/// Mirrors API Iot:Lpr section for config-key fallback without referencing ParkingApp.API.
/// Bound from the same configuration section in Infrastructure DI.
/// </summary>
public sealed class LprConfigApiKeyOptions
{
    public const string SectionName = "Iot:Lpr";

    public List<LprConfigApiKeyEntry> ApiKeys { get; set; } = new();
}

public sealed class LprConfigApiKeyEntry
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public List<string> AllowedParkingSpaceIds { get; set; } = new();
}
