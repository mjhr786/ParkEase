using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Corporate.Contracts;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Sets corporate tenant from route/header. When JWT has company_id and request has no hint,
/// binds tenant from claim (PR3). Claim vs route/header mismatch is enforced by
/// <see cref="ChannelAuthorizationMiddleware"/> when isolation is on (KD-4).
/// </summary>
public class CorporateTenantMiddleware
{
    private readonly RequestDelegate _next;

    public CorporateTenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorporateTenantContext tenantContext)
    {
        var rawCompanyId =
            context.Request.RouteValues.TryGetValue("companyId", out var routeCompanyId)
                ? routeCompanyId?.ToString()
                : null;

        rawCompanyId ??= context.Request.Headers["X-Company-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rawCompanyId)
            && context.User.Identity?.IsAuthenticated == true)
        {
            rawCompanyId = context.User.FindFirst(ParkEaseClaimTypes.CompanyId)?.Value;
        }

        if (!string.IsNullOrWhiteSpace(rawCompanyId) && Guid.TryParse(rawCompanyId, out var parsedId))
        {
            tenantContext.SetCompanyId(parsedId);
        }

        await _next(context);
    }
}
