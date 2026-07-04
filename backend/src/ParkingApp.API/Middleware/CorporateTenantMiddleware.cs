using ParkingApp.Application.Interfaces;

namespace ParkingApp.API.Middleware;

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

        if (!string.IsNullOrWhiteSpace(rawCompanyId) && Guid.TryParse(rawCompanyId, out var parsedId))
        {
            tenantContext.SetCompanyId(parsedId);
        }

        await _next(context);
    }
}
