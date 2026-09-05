using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Identity.Infrastructure.ModuleAdapters;
using ParkingApp.Identity.Infrastructure.Repositories;
using ParkingApp.Identity.Infrastructure.Services.ExternalAuth;
using ParkingApp.Identity.Infrastructure.Services.Sso;

namespace ParkingApp.Identity.Infrastructure;

/// <summary>
/// Identity module infrastructure registration (repos + outward contracts + Corporate SSO infra).
/// Host must register <c>IIdentityDbContext</c> and <c>IIdentityUnitOfWork</c> facades.
/// Host also registers <see cref="ISessionRebindService"/> (needs shared UoW + ITokenService).
/// Host binds <c>ExternalAuthOptions</c> and <c>CorporateSsoOptions</c> from configuration.
/// Host must call <c>AddDataProtection()</c> with a shared key ring for multi-node SSO secrets.
/// </summary>
public static class IdentityInfrastructureModule
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<IDeviceTokenLookup, DeviceTokenLookup>();

        // External IdP validators (Google + Apple; composite routes by provider)
        services.AddHttpClient(HttpAppleJwksKeyProvider.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "ParkEase-ExternalAuth/1.0");
        });
        services.AddSingleton<IAppleJwksKeyProvider, HttpAppleJwksKeyProvider>();
        services.AddScoped<GoogleExternalTokenValidator>();
        services.AddScoped<AppleExternalTokenValidator>();
        services.AddScoped<IExternalTokenValidator, CompositeExternalTokenValidator>();

        services.AddSingleton<ILinkPasswordAttemptTracker, LinkPasswordAttemptTracker>();

        // Corporate SSO (fail-closed stores + OIDC + secret protector)
        services.AddSingleton<ISsoSecretProtector, SsoSecretProtector>();
        services.AddSingleton<ISsoLoginStateStore, MemorySsoLoginStateStore>();
        services.AddSingleton<ISsoExchangeCodeStore, MemorySsoExchangeCodeStore>();
        services.AddHttpClient(OidcTokenService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "ParkEase-CorporateSso/1.0");
        });
        services.AddScoped<IOidcTokenService, OidcTokenService>();
        services.AddScoped<ICorporateSsoLinkAdmin, CorporateSsoLinkAdmin>();

        return services;
    }
}
