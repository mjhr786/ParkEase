using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Corporate.Domain;

/// <summary>
/// Email domain claimed by a company for Corporate SSO. Must be DNS-verified before SSO enablement.
/// </summary>
public class CompanySsoDomain : BaseEntity
{
    private static readonly Regex DomainRegex = new(
        @"^(?:[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Guid CompanyId { get; private set; }
    public Guid CompanySsoConfigurationId { get; private set; }
    public string Domain { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public string VerificationToken { get; private set; } = string.Empty;
    public DateTime? VerifiedAt { get; private set; }

    public virtual CompanySsoConfiguration Configuration { get; private set; } = null!;

    [ExcludeFromCodeCoverage]
    private CompanySsoDomain()
    {
    }

    public static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ValidationException("domain", "Domain is required");

        var normalized = domain.Trim().ToLowerInvariant().Trim('.');
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
            normalized = normalized[4..];

        if (!DomainRegex.IsMatch(normalized))
            throw new ValidationException("domain", "Invalid domain format");

        return normalized;
    }

    public static CompanySsoDomain Create(Guid companyId, Guid configurationId, string normalizedDomain)
    {
        if (companyId == Guid.Empty)
            throw new ValidationException("companyId", "Company id is required");
        if (configurationId == Guid.Empty)
            throw new ValidationException("configurationId", "Configuration id is required");

        var domain = NormalizeDomain(normalizedDomain);
        return new CompanySsoDomain
        {
            CompanyId = companyId,
            CompanySsoConfigurationId = configurationId,
            Domain = domain,
            IsVerified = false,
            VerificationToken = GenerateToken(),
            VerifiedAt = null
        };
    }

    /// <summary>Expected DNS TXT value: parkease-sso-verify={token}</summary>
    public string ExpectedTxtValue => $"parkease-sso-verify={VerificationToken}";

    /// <summary>Preferred DNS host: _parkease-sso.{domain}</summary>
    public string PreferredTxtHost => $"_parkease-sso.{Domain}";

    public void MarkVerified(DateTime? verifiedAtUtc = null)
    {
        IsVerified = true;
        VerifiedAt = verifiedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unverify()
    {
        IsVerified = false;
        VerifiedAt = null;
        VerificationToken = GenerateToken();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateVerificationToken()
    {
        VerificationToken = GenerateToken();
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
