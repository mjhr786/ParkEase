using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.API.Filters;

/// <summary>
/// Validates X-Api-Key against facility DB camera keys (primary) and config fallback keys.
/// Sets HttpContext.Items for KeyId and allowed facility GUIDs.
/// </summary>
public sealed class IotApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";
    public const string KeyIdItemName = "IotKeyId";
    public const string AllowedSpacesItemName = "IotAllowedParkingSpaceIds";

    private readonly ILprCameraKeyAuthenticator _authenticator;

    public IotApiKeyAuthorizationFilter(ILprCameraKeyAuthenticator authenticator)
    {
        _authenticator = authenticator;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false,
                message = "Missing X-Api-Key header"
            });
            return;
        }

        var result = await _authenticator.AuthenticateAsync(
            provided.ToString(),
            context.HttpContext.RequestAborted);

        if (result is null)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false,
                message = "Invalid API key"
            });
            return;
        }

        context.HttpContext.Items[KeyIdItemName] = result.KeyId;
        context.HttpContext.Items[AllowedSpacesItemName] = result.AllowedParkingSpaceIds;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IotApiKeyAttribute : TypeFilterAttribute
{
    public IotApiKeyAttribute() : base(typeof(IotApiKeyAuthorizationFilter))
    {
    }
}
