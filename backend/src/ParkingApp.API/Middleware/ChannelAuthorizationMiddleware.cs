using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ParkingApp.API.Options;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Security;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Authoritative channel allow/deny matrix (KD-5). ASP.NET policies are optional defense-in-depth only.
/// Pipeline: UseAuthentication → UseAuthorization → CorporateTenantMiddleware → this → MapControllers.
/// </summary>
public sealed class ChannelAuthorizationMiddleware
{
    public const string ChannelForbiddenCode = "channel_forbidden";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ChannelAuthorizationMiddleware> _logger;

    public ChannelAuthorizationMiddleware(
        RequestDelegate next,
        ILogger<ChannelAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<ChannelIsolationOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;

        // KD-10: flag off → no-op
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        // KD-21: anonymous skip (public parking/search etc.)
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        // Only enforce matrix for API and SignalR hubs
        if (!IsEnforcedPath(path))
        {
            await _next(context);
            return;
        }

        // KD-5a / KD-13: platform Admin role fast-path for admin + outbox surfaces
        if (IsPlatformAdmin(context.User) && IsAdminSurfacePath(path))
        {
            await _next(context);
            return;
        }

        var channel = ResolveChannel(context.User, options);
        var companyIdClaim = TryGetCompanyId(context.User);
        var hasCompanyId = companyIdClaim.HasValue;
        var channelAccess = ChannelRouteMatrix.AccessForChannel(channel, hasCompanyId);

        var rule = ChannelRouteMatrix.FindMatch(method, path);
        if (rule is null)
        {
            await DenyAsync(context, method, path, channel, "no_matching_rule");
            return;
        }

        if (rule.IsVendorAllowlist && !options.VendorAllocationAllowlistEnabled)
        {
            await DenyAsync(context, method, path, channel, "vendor_allowlist_disabled");
            return;
        }

        var isPlatformAdmin = IsPlatformAdmin(context.User);
        var allowed = IsAccessAllowed(rule, channelAccess, isPlatformAdmin, context.User);
        if (!allowed)
        {
            await DenyAsync(context, method, path, channel, "channel_not_allowed", rule.Id);
            return;
        }

        // Corporate + company_id: enforce match with route / X-Company-Id when present (KD-4)
        if (options.EnforceCompanyClaimMatch
            && channel == ProductChannel.Corporate
            && companyIdClaim.HasValue
            && (rule.EnforceCompanyIdMatch || RequestHasCompanyHint(context)))
        {
            var requestCompanyId = ResolveRequestCompanyId(context);
            if (requestCompanyId.HasValue && requestCompanyId.Value != companyIdClaim.Value)
            {
                await DenyAsync(context, method, path, channel, "company_mismatch", rule.Id);
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Evaluates rule access for the caller's channel bit and optional CA / platform-admin.
    /// <see cref="ChannelRouteRule.RequireCompanyAdmin"/> applies only when the caller is CorporateBound.
    /// </summary>
    internal static bool IsAccessAllowed(
        ChannelRouteRule rule,
        ChannelAccess channelAccess,
        bool isPlatformAdmin,
        ClaimsPrincipal user)
    {
        if (isPlatformAdmin && rule.Access.HasFlag(ChannelAccess.PlatformAdminRole))
            return true;

        if (!rule.Access.HasFlag(channelAccess) || channelAccess == ChannelAccess.None)
            return false;

        // CA (KD-23): Corporate bound + lease-browse style rules require company_role=Admin
        if (rule.RequireCompanyAdmin && channelAccess == ChannelAccess.CorporateBound)
        {
            var companyRole = user.FindFirst(ParkEaseClaimTypes.CompanyRole)?.Value;
            if (!string.Equals(companyRole, "Admin", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    internal static ProductChannel ResolveChannel(ClaimsPrincipal user, ChannelIsolationOptions options)
    {
        var raw = user.FindFirst(ParkEaseClaimTypes.Channel)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Soak default: TreatMissingClaimAs=Marketplace (document); Reject after cutover.
            if (string.Equals(options.TreatMissingClaimAs, "Reject", StringComparison.OrdinalIgnoreCase))
                return (ProductChannel)0; // None — will fail access

            return ProductChannel.Marketplace;
        }

        if (Enum.TryParse<ProductChannel>(raw, ignoreCase: true, out var channel))
            return channel;

        return ProductChannel.Marketplace;
    }

    private static bool IsEnforcedPath(string path) =>
        path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminSurfacePath(string path) =>
        path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/outbox", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlatformAdmin(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("admin");

    private static Guid? TryGetCompanyId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ParkEaseClaimTypes.CompanyId)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static bool RequestHasCompanyHint(HttpContext context) =>
        ResolveRequestCompanyId(context).HasValue;

    private static Guid? ResolveRequestCompanyId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("companyId", out var routeVal)
            && routeVal is not null
            && Guid.TryParse(routeVal.ToString(), out var fromRoute))
        {
            return fromRoute;
        }

        var header = context.Request.Headers["X-Company-Id"].FirstOrDefault();
        if (Guid.TryParse(header, out var fromHeader))
            return fromHeader;

        // Path fallback: /api/v1/corporate/companies/{guid}/...
        var path = context.Request.Path.Value ?? "";
        var marker = "/api/v1/corporate/companies/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = path[(idx + marker.Length)..];
            var segment = rest.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (Guid.TryParse(segment, out var fromPath))
                return fromPath;
        }

        return null;
    }

    private async Task DenyAsync(
        HttpContext context,
        string method,
        string path,
        ProductChannel channel,
        string reason,
        string? ruleId = null)
    {
        _logger.LogWarning(
            "ChannelIsolation denied: {Reason} method={Method} path={Path} channel={Channel} rule={RuleId} user={User}",
            reason,
            method,
            path,
            channel,
            ruleId ?? "(none)",
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity?.Name ?? "(unknown)");

        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new ApiResponse<object>(
            Success: false,
            Message: "Access denied for the current product channel.",
            Data: null,
            Errors: new List<string> { ChannelForbiddenCode },
            Code: ChannelForbiddenCode);

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
