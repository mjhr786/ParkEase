using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingApp.Corporate.Contracts;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>
/// Loads company OIDC client secret ciphertext and unprotects via Data Protection
/// purpose <c>ParkEase.CorporateSso.Secrets.v1</c> (must match ISsoSecretProtector).
/// </summary>
public sealed class SsoClientSecretAccessor : ISsoClientSecretAccessor
{
    public const string ProtectionPurpose = "ParkEase.CorporateSso.Secrets.v1";
    public const string SecretUnreadableCode = "SecretUnreadable";

    private readonly ICorporateDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<SsoClientSecretAccessor> _logger;

    public SsoClientSecretAccessor(
        ICorporateDbContext db,
        IDataProtectionProvider dataProtection,
        ILogger<SsoClientSecretAccessor> logger)
    {
        _db = db;
        _protector = dataProtection.CreateProtector(ProtectionPurpose);
        _logger = logger;
    }

    public async Task<(bool Ok, string? Secret)> TryGetClientSecretAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var cfg = await _db.CompanySsoConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && !c.IsDeleted, cancellationToken);

        if (cfg is null || string.IsNullOrWhiteSpace(cfg.ClientSecretProtected))
        {
            _logger.LogWarning("SSO client secret missing for company {CompanyId}", companyId);
            return (false, null);
        }

        try
        {
            var plaintext = _protector.Unprotect(cfg.ClientSecretProtected);
            if (string.IsNullOrEmpty(plaintext))
                return (false, null);
            return (true, plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SSO client secret unreadable for company {CompanyId} ({Code})",
                companyId,
                SecretUnreadableCode);
            return (false, null);
        }
    }
}
