using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using ParkingApp.Corporate.Application.Interfaces;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>
/// Data Protection wrapper for company admin secret write/read.
/// Purpose must match Identity SsoSecretProtector and SsoClientSecretAccessor.
/// </summary>
public sealed class SsoSecretCipher : ISsoSecretCipher
{
    public const string Purpose = "ParkEase.CorporateSso.Secrets.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<SsoSecretCipher> _logger;

    public SsoSecretCipher(IDataProtectionProvider provider, ILogger<SsoSecretCipher> logger)
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
            _logger.LogError(ex, "Failed to unprotect Corporate SSO secret ({Code})", ISsoSecretCipher.SecretUnreadableCode);
            return false;
        }
    }
}
