using ParkingApp.BuildingBlocks.Security;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Which product channels / roles a route allows under the isolation matrix.
/// </summary>
[Flags]
public enum ChannelAccess
{
    None = 0,
    Marketplace = 1 << 0,
    /// <summary>Corporate channel with JWT <c>company_id</c> bound.</summary>
    CorporateBound = 1 << 1,
    /// <summary>Corporate channel without <c>company_id</c> (founder bootstrap).</summary>
    CorporateBootstrap = 1 << 2,
    /// <summary>Admin product channel (shell).</summary>
    AdminChannel = 1 << 3,
    /// <summary>Platform <c>UserRole.Admin</c> on any channel (matrix column A / KD-13).</summary>
    PlatformAdminRole = 1 << 4,

    /// <summary>Auth / profile endpoints shared across shells.</summary>
    AllProductChannels = Marketplace | CorporateBound | CorporateBootstrap | AdminChannel,

    /// <summary>Bound corporate + marketplace vendor/common (no bootstrap).</summary>
    MarketplaceAndCorporateBound = Marketplace | CorporateBound,

    /// <summary>Chat / notifications / hubs when bound (not bootstrap).</summary>
    MarketplaceCorporateBoundAdmin = Marketplace | CorporateBound | AdminChannel,
}

/// <summary>
/// One row of the authoritative channel allowlist (KD-5). Matched by HTTP method + path pattern.
/// Shared by middleware and theory tests — do not duplicate rules in tests.
/// </summary>
/// <param name="Id">Stable rule id for tests/logs.</param>
/// <param name="HttpMethod">HTTP verb, or <c>*</c> for any method.</param>
/// <param name="PathPattern">
/// Path template. Segments <c>{name}</c> match one path segment;
/// trailing <c>/**</c> matches zero or more remaining segments (prefix).
/// </param>
/// <param name="Access">Allowed channels / platform-admin role.</param>
/// <param name="RequireCompanyAdmin">CA: require JWT <c>company_role=Admin</c> (KD-23).</param>
/// <param name="EnforceCompanyIdMatch">When corporate-bound, require claim company_id matches route/header when present.</param>
/// <param name="IsVendorAllowlist">Vendor B2B allowlist (KD-6); gated by options.</param>
public sealed record ChannelRouteRule(
    string Id,
    string HttpMethod,
    string PathPattern,
    ChannelAccess Access,
    bool RequireCompanyAdmin = false,
    bool EnforceCompanyIdMatch = false,
    bool IsVendorAllowlist = false);

/// <summary>
/// Static matrix builder + path matcher used by <see cref="ChannelAuthorizationMiddleware"/>.
/// </summary>
public static class ChannelRouteMatrix
{
    /// <summary>Authoritative rule table (order = first match wins). Tests must iterate this collection.</summary>
    public static IReadOnlyList<ChannelRouteRule> Rules { get; } = BuildRules();

    public static ChannelRouteRule? FindMatch(string httpMethod, string path)
    {
        foreach (var rule in Rules)
        {
            if (MethodMatches(httpMethod, rule.HttpMethod) && PathMatches(path, rule.PathPattern))
                return rule;
        }

        return null;
    }

    public static bool MethodMatches(string requestMethod, string ruleMethod)
    {
        if (string.IsNullOrEmpty(ruleMethod) || ruleMethod == "*")
            return true;
        return string.Equals(requestMethod, ruleMethod, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Matches request path against a template with <c>{param}</c> segments and optional trailing <c>/**</c>.
    /// </summary>
    public static bool PathMatches(string requestPath, string pattern)
    {
        var path = NormalizePath(requestPath);
        var pat = NormalizePath(pattern);

        var multi = pat.EndsWith("/**", StringComparison.Ordinal);
        if (multi)
            pat = pat[..^3];
        if (pat.Length == 0)
            pat = "/";

        var pathSegs = SplitSegments(path);
        var patSegs = SplitSegments(pat);

        if (multi)
        {
            if (pathSegs.Length < patSegs.Length)
                return false;
            for (var i = 0; i < patSegs.Length; i++)
            {
                if (!SegmentMatches(pathSegs[i], patSegs[i]))
                    return false;
            }

            return true;
        }

        if (pathSegs.Length != patSegs.Length)
            return false;
        for (var i = 0; i < patSegs.Length; i++)
        {
            if (!SegmentMatches(pathSegs[i], patSegs[i]))
                return false;
        }

        return true;
    }

    /// <summary>Sample concrete path for theory tests (replaces <c>{param}</c> with a fixed GUID).</summary>
    public static string SamplePath(string pathPattern)
    {
        const string guid = "11111111-1111-1111-1111-111111111111";
        var multi = pathPattern.Contains("/**", StringComparison.Ordinal);
        var path = pathPattern.Replace("/**", "", StringComparison.Ordinal);
        // Replace {anything} with guid
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segs.Length; i++)
        {
            if (segs[i].StartsWith('{') && segs[i].EndsWith('}'))
                segs[i] = guid;
        }

        var result = "/" + string.Join('/', segs);
        // Ensure /** patterns retain a trailing segment so they don't collide with exact parents.
        if (multi)
            result = result.TrimEnd('/') + "/sample";
        return result;
    }

    public static ChannelAccess AccessForChannel(ProductChannel channel, bool hasCompanyId) =>
        channel switch
        {
            ProductChannel.Marketplace => ChannelAccess.Marketplace,
            ProductChannel.Admin => ChannelAccess.AdminChannel,
            ProductChannel.Corporate => hasCompanyId
                ? ChannelAccess.CorporateBound
                : ChannelAccess.CorporateBootstrap,
            _ => ChannelAccess.None
        };

    private static IReadOnlyList<ChannelRouteRule> BuildRules()
    {
        // More specific rules first. Default deny when no rule matches (KD-21).
        var rules = new List<ChannelRouteRule>
        {
            // ── Auth (all channels; public login/register handled by anonymous skip) ──
            new("auth-login", "POST", "/api/auth/login", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-register", "POST", "/api/auth/register", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-refresh", "POST", "/api/auth/refresh", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-logout", "POST", "/api/auth/logout", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-change-password", "POST", "/api/auth/change-password", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-login-corporate", "POST", "/api/auth/login/corporate", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-channel", "POST", "/api/auth/channel", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-channel-context", "GET", "/api/auth/channel-context", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("auth-prefix", "*", "/api/auth/**", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),

            // ── Users / vehicles / device tokens ──
            new("users-me", "*", "/api/users/me", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("users-prefix", "*", "/api/users/**", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("vehicles", "*", "/api/vehicles/**", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("vehicles-root", "*", "/api/vehicles", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("device-tokens", "*", "/api/device-tokens/**", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),
            new("device-tokens-root", "*", "/api/device-tokens", ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),

            // ── Admin APIs (platform role A only; KD-5a fast-path also covers these paths) ──
            new("admin-apis", "*", "/api/admin/**", ChannelAccess.PlatformAdminRole),
            new("outbox-legacy", "*", "/api/outbox/**", ChannelAccess.PlatformAdminRole),

            // ── Corporate vendor allowlist (KD-6) — Marketplace only ──
            new("corp-vendor-allocations", "GET", "/api/v1/corporate/vendor/allocations",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole, IsVendorAllowlist: true),
            new("corp-alloc-approve", "POST", "/api/v1/corporate/allocations/{allocationId}/approve",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole, IsVendorAllowlist: true),
            new("corp-alloc-reject", "POST", "/api/v1/corporate/allocations/{allocationId}/reject",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole, IsVendorAllowlist: true),

            // ── Corporate invite accept (all channels) ──
            new("corp-invite-accept", "POST", "/api/v1/corporate/invitations/accept",
                ChannelAccess.AllProductChannels | ChannelAccess.PlatformAdminRole),

            // ── Corporate me/companies (Marketplace CTA L§ + corporate/bootstrap) ──
            new("corp-me-companies", "GET", "/api/v1/corporate/me/companies",
                ChannelAccess.Marketplace | ChannelAccess.CorporateBound | ChannelAccess.CorporateBootstrap | ChannelAccess.AdminChannel | ChannelAccess.PlatformAdminRole),

            // ── Create company (bound + bootstrap B; not Marketplace) ──
            new("corp-create-company", "POST", "/api/v1/corporate/companies",
                ChannelAccess.CorporateBound | ChannelAccess.CorporateBootstrap | ChannelAccess.PlatformAdminRole),

            // ── Company-scoped corporate APIs (bound + company match; not bootstrap) ──
            new("corp-company-scoped", "*", "/api/v1/corporate/companies/{companyId}/**",
                ChannelAccess.CorporateBound | ChannelAccess.PlatformAdminRole,
                EnforceCompanyIdMatch: true),
            new("corp-company-root", "*", "/api/v1/corporate/companies/{companyId}",
                ChannelAccess.CorporateBound | ChannelAccess.PlatformAdminRole,
                EnforceCompanyIdMatch: true),

            // ── Catch remaining corporate API under v1 (deny marketplace unless listed above) ──
            // Bound corporate only for any leftover corporate paths
            new("corp-remainder", "*", "/api/v1/corporate/**",
                ChannelAccess.CorporateBound | ChannelAccess.PlatformAdminRole,
                EnforceCompanyIdMatch: true),

            // ── Parking discovery (CA lease-browse for Corporate bound) ──
            new("parking-search", "GET", "/api/parking/search",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole | ChannelAccess.CorporateBound,
                RequireCompanyAdmin: true),
            // Note: RequireCompanyAdmin applies only when evaluating CorporateBound; see middleware.
            new("parking-map", "GET", "/api/parking/map",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole | ChannelAccess.CorporateBound,
                RequireCompanyAdmin: true),
            new("parking-by-id", "GET", "/api/parking/{id}",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole | ChannelAccess.CorporateBound,
                RequireCompanyAdmin: true),
            new("parking-my-listings", "GET", "/api/parking/my-listings",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("parking-create", "POST", "/api/parking",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("parking-owner-mutations", "*", "/api/parking/{id}/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("parking-prefix", "*", "/api/parking/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── LPR registry under parking ──
            new("lpr-registry", "*", "/api/parking/{parkingSpaceId}/lpr/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Parking availability (CA read for corporate) ──
            new("parking-availability", "*", "/api/parking-availability/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole | ChannelAccess.CorporateBound,
                RequireCompanyAdmin: true),
            new("parking-availability-root", "*", "/api/parking-availability",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole | ChannelAccess.CorporateBound,
                RequireCompanyAdmin: true),

            // ── Bookings / access-pass ──
            new("bookings", "*", "/api/bookings/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("bookings-root", "*", "/api/bookings",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Payments ──
            new("payments", "*", "/api/payments/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("payments-root", "*", "/api/payments",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Reviews ──
            new("reviews", "*", "/api/reviews/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("reviews-root", "*", "/api/reviews",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Dashboard (marketplace vendor/member) ──
            new("dashboard", "*", "/api/dashboard/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("dashboard-root", "*", "/api/dashboard",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Favorites (marketplace only; no platform-admin A) ──
            new("favorites", "*", "/api/favorites/**", ChannelAccess.Marketplace),
            new("favorites-root", "*", "/api/favorites", ChannelAccess.Marketplace),

            // ── Passes ──
            new("passes", "*", "/api/passes/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("passes-root", "*", "/api/passes",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Event packages ──
            new("event-packages", "*", "/api/event-packages/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("event-packages-root", "*", "/api/event-packages",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Ancillary services ──
            new("ancillary", "*", "/api/ancillary-services/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("ancillary-root", "*", "/api/ancillary-services",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),

            // ── Files (Marketplace + Corporate L‡ including bootstrap) ──
            new("files", "*", "/api/files/**",
                ChannelAccess.Marketplace | ChannelAccess.CorporateBound | ChannelAccess.CorporateBootstrap | ChannelAccess.PlatformAdminRole),
            new("files-root", "*", "/api/files",
                ChannelAccess.Marketplace | ChannelAccess.CorporateBound | ChannelAccess.CorporateBootstrap | ChannelAccess.PlatformAdminRole),

            // ── Chat / notifications (Marketplace + Corporate bound + Admin; not bootstrap) ──
            new("chat", "*", "/api/chat/**",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),
            new("chat-root", "*", "/api/chat",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),
            new("notifications", "*", "/api/notifications/**",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),
            new("notifications-root", "*", "/api/notifications",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),

            // ── SignalR hubs ──
            new("hubs", "*", "/hubs/**",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),
            new("hubs-root", "*", "/hubs",
                ChannelAccess.MarketplaceCorporateBoundAdmin | ChannelAccess.PlatformAdminRole),

            // ── IoT (Marketplace owner L + Admin) ──
            new("iot-ocpp", "*", "/api/iot/ocpp/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("iot", "*", "/api/iot/**",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
            new("iot-root", "*", "/api/iot",
                ChannelAccess.Marketplace | ChannelAccess.PlatformAdminRole),
        };

        return rules;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";
        var p = path.Trim();
        if (!p.StartsWith('/'))
            p = "/" + p;
        // Keep /** intact; only trim a single trailing slash when not **
        if (p.Length > 1 && p.EndsWith('/') && !p.EndsWith("**/", StringComparison.Ordinal) && !p.EndsWith("/**", StringComparison.Ordinal))
            p = p.TrimEnd('/');
        return p;
    }

    private static string[] SplitSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool SegmentMatches(string actual, string patternSegment)
    {
        if (patternSegment.Length >= 2 && patternSegment[0] == '{' && patternSegment[^1] == '}')
            return actual.Length > 0;
        return string.Equals(actual, patternSegment, StringComparison.OrdinalIgnoreCase);
    }
}
