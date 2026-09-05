using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using ParkingApp.Identity.Application.Interfaces;

namespace ParkingApp.Identity.Infrastructure.Services.Sso;

/// <summary>Data Protection wrapper for Corporate SSO IdP secrets (multi-node key ring required).</summary>
public sealed class SsoSecretProtector : ISsoSecretProtector
{
    public const string Purpose = "ParkEase.CorporateSso.Secrets.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<SsoSecretProtector> _logger;

    public SsoSecretProtector(IDataProtectionProvider provider, ILogger<SsoSecretProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext secret is required", nameof(plaintext));
        return _protector.Protect(plaintext);
    }

    public bool TryUnprotect(string protectedCiphertext, out string? plaintext)
    {
        plaintext = null;
        if (string.IsNullOrWhiteSpace(protectedCiphertext))
            return false;

        try
        {
            plaintext = _protector.Unprotect(protectedCiphertext);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect Corporate SSO secret ({Code})", ISsoSecretProtector.SecretUnreadableCode);
            return false;
        }
    }
}
