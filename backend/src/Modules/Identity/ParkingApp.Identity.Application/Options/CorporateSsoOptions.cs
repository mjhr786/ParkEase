namespace ParkingApp.Identity.Application.Options;

/// <summary>Master options for Corporate SSO (OIDC MVP). Bound from configuration section "CorporateSso".</summary>
public sealed class CorporateSsoOptions
{
    public const string SectionName = "CorporateSso";

    /// <summary>
    /// Global kill switch. Default false. Ops enables only after the corporate-sso-runbook GA checklist;
    /// pilot company E2E is an ops step, not a code merge gate.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Corporate SPA origin used for web post-callback redirects (no trailing slash).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Public API base URL used to build OIDC redirect_uri (no trailing slash).</summary>
    public string PublicApiBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>Allowlisted mobile complete deep-link prefixes. Default canonical URI only.</summary>
    public List<string> MobileRedirectUris { get; set; } = new()
    {
        "parkease://corporate/sso/complete"
    };

    /// <summary>Optional company allow-list for staged rollout. Empty = all companies.</summary>
    public List<Guid> AllowedCompanyIds { get; set; } = new();

    /// <summary>SSO login state TTL in seconds (default 10 minutes).</summary>
    public int StateTtlSeconds { get; set; } = 600;

    /// <summary>One-time exchange code TTL in seconds (default 60).</summary>
    public int ExchangeCodeTtlSeconds { get; set; } = 60;

    /// <summary>Clock skew for id_token exp/nbf validation.</summary>
    public int ClockSkewSeconds { get; set; } = 120;

    /// <summary>Default token endpoint client auth method when company config does not override.</summary>
    public string DefaultTokenEndpointAuthMethod { get; set; } = "client_secret_post";

    /// <summary>Per-IP rate limit for /api/auth/corporate/sso/** paths (SSO-only bucket).</summary>
    public int RateLimitPerMinute { get; set; } = 15;

    /// <summary>
    /// State store backend: Memory | Database | Redis.
    /// Production multi-node should use Redis or Database (fail-closed). Never ICacheService.
    /// </summary>
    public string StateStore { get; set; } = "Memory";

    /// <summary>Data Protection key ring path for file-system provider (dev). Shared volume/Blob in prod.</summary>
    public string? DataProtectionKeysPath { get; set; }

    /// <summary>Canonical OIDC callback path (appended to PublicApiBaseUrl).</summary>
    public const string CallbackPath = "/api/auth/corporate/sso/callback";

    /// <summary>Builds the exact registered OIDC redirect_uri for this environment.</summary>
    public string GetOidcRedirectUri()
    {
        var baseUrl = (PublicApiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return $"{baseUrl}{CallbackPath}";
    }

    /// <summary>Canonical mobile complete URI (first allowlist entry or default).</summary>
    public string GetCanonicalMobileCompleteUri()
    {
        if (MobileRedirectUris is { Count: > 0 } && !string.IsNullOrWhiteSpace(MobileRedirectUris[0]))
            return MobileRedirectUris[0].Trim();
        return "parkease://corporate/sso/complete";
    }

    public bool IsCompanyAllowed(Guid companyId)
    {
        if (AllowedCompanyIds is null || AllowedCompanyIds.Count == 0)
            return true;
        return AllowedCompanyIds.Contains(companyId);
    }
}
