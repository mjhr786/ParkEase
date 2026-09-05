namespace ParkingApp.Corporate.Contracts;

/// <summary>
/// Cross-module read of company SSO policy flags (Contracts only — no Domain types).
/// Used by Identity password gate (PR4) and SSO discover/start (PR3b).
/// </summary>
public interface ICompanySsoConfigLookup
{
    Task<CompanySsoConfigSnapshot?> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Resolve company by verified email domain (normalized lowercase, no leading @).
    /// Returns at most one company because verified domains are globally unique.
    /// </summary>
    Task<CompanySsoConfigSnapshot?> GetByVerifiedDomainAsync(string domain, CancellationToken ct = default);

    /// <summary>Resolve company by public slug (case-insensitive).</summary>
    Task<CompanySsoConfigSnapshot?> GetBySlugAsync(string slug, CancellationToken ct = default);
}

/// <param name="AutoAcceptInvitationsOnSso">Retained for forward compatibility; MVP invite accept is always attempted (KD-CS-29).</param>
/// <param name="IsEffectivelyEnabled">
/// Host-evaluated effective enablement when global flag + AllowedCompanyIds are applied at call site.
/// Infrastructure sets <c>IsEnabled &amp;&amp; !ForceDisabledByPlatform &amp;&amp; HasVerifiedDomain</c>;
/// Identity handlers AND with <c>CorporateSso:Enabled</c> and allow-list.
/// </param>
public sealed record CompanySsoConfigSnapshot(
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    bool IsEnabled,
    bool ForceDisabledByPlatform,
    bool PasswordLoginAllowed,
    bool AutoAcceptInvitationsOnSso,
    bool HasVerifiedDomain,
    bool IsEffectivelyEnabled,
    string? Authority = null,
    string? ClientId = null,
    string? MetadataUrl = null,
    string? TokenEndpointAuthMethod = null,
    bool ForceAuthn = false,
    IReadOnlyList<string>? VerifiedDomains = null);
