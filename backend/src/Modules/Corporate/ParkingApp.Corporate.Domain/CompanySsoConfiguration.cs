using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Domain.Enums;

namespace ParkingApp.Corporate.Domain;

/// <summary>
/// Per-company enterprise SSO configuration (one active OIDC config per company in MVP).
/// Secrets are stored as Data Protection ciphertext only.
/// </summary>
public class CompanySsoConfiguration : BaseEntity
{
    private static readonly Regex AuthorityRegex = new(
        @"^https://[^\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Guid CompanyId { get; private set; }
    public SsoProtocol Protocol { get; private set; } = SsoProtocol.Oidc;
    public bool IsEnabled { get; private set; }
    public bool ForceDisabledByPlatform { get; private set; }
    public DateTime? ForceDisabledAt { get; private set; }
    public Guid? ForceDisabledByUserId { get; private set; }

    public string Authority { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string? ClientSecretProtected { get; private set; }
    public string? MetadataUrl { get; private set; }
    public string? AttributeMappingJson { get; private set; }
    public string? TokenEndpointAuthMethod { get; private set; }

    public bool PasswordLoginAllowed { get; private set; } = true;
    public bool AutoAcceptInvitationsOnSso { get; private set; } = true;
    public bool ForceAuthn { get; private set; }
    public bool AllowJitProvisioning { get; private set; }
    public bool AllowIdpInitiated { get; private set; }

    public string? SpEntityId { get; private set; }
    public string? AcsUrlOverride { get; private set; }
    public string? IdpSigningCertProtected { get; private set; }
    public string? IdpSigningCertSecondaryProtected { get; private set; }

    public DateTime? EnabledAt { get; private set; }
    public DateTime? DisabledAt { get; private set; }
    public DateTime? LastTestedAt { get; private set; }
    public string? LastTestResult { get; private set; }

    public virtual Company Company { get; private set; } = null!;
    public virtual ICollection<CompanySsoDomain> Domains { get; private set; } = new List<CompanySsoDomain>();

    [ExcludeFromCodeCoverage]
    private CompanySsoConfiguration()
    {
    }

    public static CompanySsoConfiguration Create(Guid companyId, SsoProtocol protocol = SsoProtocol.Oidc)
    {
        if (companyId == Guid.Empty)
            throw new ValidationException("companyId", "Company id is required");

        return new CompanySsoConfiguration
        {
            CompanyId = companyId,
            Protocol = protocol,
            IsEnabled = false,
            PasswordLoginAllowed = true,
            AutoAcceptInvitationsOnSso = true,
            // Placeholder until UpsertOidcSettings; Enable() requires real Authority/ClientId/secret.
            Authority = string.Empty,
            ClientId = string.Empty
        };
    }

    public void UpsertOidcSettings(
        string authority,
        string clientId,
        string? clientSecretProtected,
        string? metadataUrl,
        string? attributeMappingJson,
        string? tokenEndpointAuthMethod,
        bool passwordLoginAllowed,
        bool autoAcceptInvitationsOnSso,
        bool forceAuthn,
        bool allowJitProvisioning)
    {
        if (string.IsNullOrWhiteSpace(authority) || !AuthorityRegex.IsMatch(authority.Trim()))
            throw new ValidationException("authority", "Authority must be an https URL");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ValidationException("clientId", "Client id is required");

        if (!string.IsNullOrWhiteSpace(tokenEndpointAuthMethod)
            && tokenEndpointAuthMethod is not ("client_secret_post" or "client_secret_basic"))
        {
            throw new ValidationException(
                "tokenEndpointAuthMethod",
                "TokenEndpointAuthMethod must be client_secret_post or client_secret_basic");
        }

        Authority = authority.Trim().TrimEnd('/');
        ClientId = clientId.Trim();
        if (clientSecretProtected is not null)
            ClientSecretProtected = string.IsNullOrWhiteSpace(clientSecretProtected) ? null : clientSecretProtected;
        MetadataUrl = string.IsNullOrWhiteSpace(metadataUrl) ? null : metadataUrl.Trim();
        AttributeMappingJson = string.IsNullOrWhiteSpace(attributeMappingJson) ? null : attributeMappingJson;
        TokenEndpointAuthMethod = string.IsNullOrWhiteSpace(tokenEndpointAuthMethod)
            ? null
            : tokenEndpointAuthMethod.Trim();
        PasswordLoginAllowed = passwordLoginAllowed;
        AutoAcceptInvitationsOnSso = autoAcceptInvitationsOnSso;
        ForceAuthn = forceAuthn;
        AllowJitProvisioning = allowJitProvisioning;
        Protocol = SsoProtocol.Oidc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Sets encrypted client secret ciphertext. Pass null to leave unchanged; empty clears.</summary>
    public void SetClientSecretProtected(string? protectedCiphertext)
    {
        ClientSecretProtected = string.IsNullOrWhiteSpace(protectedCiphertext) ? null : protectedCiphertext;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTestResult(string resultCode)
    {
        LastTestedAt = DateTime.UtcNow;
        LastTestResult = string.IsNullOrWhiteSpace(resultCode) ? null : resultCode.Trim()[..Math.Min(resultCode.Trim().Length, 64)];
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        if (ForceDisabledByPlatform)
            throw new BusinessRuleException("sso_force_disabled", "SSO is force-disabled by platform and cannot be enabled");

        if (string.IsNullOrWhiteSpace(Authority) || string.IsNullOrWhiteSpace(ClientId))
            throw new BusinessRuleException("sso_config_incomplete", "Authority and ClientId are required before enable");

        if (string.IsNullOrWhiteSpace(ClientSecretProtected))
            throw new BusinessRuleException("sso_secret_required", "Client secret is required before enable");

        if (!Domains.Any(d => d.IsVerified && !d.IsDeleted))
            throw new BusinessRuleException("sso_domain_required", "At least one verified domain is required before enable");

        IsEnabled = true;
        EnabledAt = DateTime.UtcNow;
        DisabledAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        DisabledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Platform sticky lock: forces off; company admin cannot re-enable until cleared.</summary>
    public void ForceDisableByPlatform(Guid platformAdminUserId)
    {
        if (platformAdminUserId == Guid.Empty)
            throw new ValidationException("platformAdminUserId", "Platform admin user id is required");

        ForceDisabledByPlatform = true;
        ForceDisabledAt = DateTime.UtcNow;
        ForceDisabledByUserId = platformAdminUserId;
        IsEnabled = false;
        DisabledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Platform-only: clears force lock; does not auto re-enable SSO.</summary>
    public void ClearForceDisableByPlatform()
    {
        ForceDisabledByPlatform = false;
        ForceDisabledAt = null;
        ForceDisabledByUserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public CompanySsoDomain AddDomain(string domain)
    {
        var normalized = CompanySsoDomain.NormalizeDomain(domain);
        if (Domains.Any(d => !d.IsDeleted && string.Equals(d.Domain, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("domain_exists", $"Domain {normalized} is already configured");

        var entity = CompanySsoDomain.Create(CompanyId, Id, normalized);
        Domains.Add(entity);
        UpdatedAt = DateTime.UtcNow;
        return entity;
    }

    /// <summary>
    /// Soft-removes a claimed domain. If SSO is enabled and this was the last verified domain, disables SSO.
    /// </summary>
    public void RemoveDomain(Guid domainId)
    {
        var domain = Domains.FirstOrDefault(d => d.Id == domainId && !d.IsDeleted)
            ?? throw new BusinessRuleException("domain_not_found", "SSO domain was not found");

        domain.IsDeleted = true;
        domain.UpdatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (IsEnabled && !Domains.Any(d => d.IsVerified && !d.IsDeleted))
            Disable();
    }

    public CompanySsoDomain RequireDomain(Guid domainId)
    {
        return Domains.FirstOrDefault(d => d.Id == domainId && !d.IsDeleted)
            ?? throw new BusinessRuleException("domain_not_found", "SSO domain was not found");
    }

    public bool HasClientSecret => !string.IsNullOrWhiteSpace(ClientSecretProtected);
}

