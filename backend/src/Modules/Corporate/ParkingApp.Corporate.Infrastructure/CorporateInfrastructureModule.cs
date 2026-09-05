using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Infrastructure.ModuleAdapters;
using ParkingApp.Corporate.Infrastructure.ReadStores;
using ParkingApp.Corporate.Infrastructure.Services;

namespace ParkingApp.Corporate.Infrastructure;

/// <summary>
/// Corporate module infrastructure: company repos, read stores, quota cache, tenant context, waitlist promotion, SSO.
/// </summary>
public static class CorporateInfrastructureModule
{
    public static IServiceCollection AddCorporateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICompanyReadStore, CompanyReadStore>();
        services.AddScoped<ICompanyMembershipLookup, CompanyMembershipLookup>();
        services.AddScoped<ICompanySsoConfigLookup, CompanySsoConfigLookup>();
        services.AddScoped<ICorporateSsoMembershipProvisioner, CorporateSsoMembershipProvisioner>();
        services.AddScoped<ISsoClientSecretAccessor, SsoClientSecretAccessor>();
        services.AddScoped<ICorporateTenantContext, CorporateTenantContext>();
        services.AddScoped<ICompanyQuotaCache, CompanyQuotaCache>();
        services.AddSingleton<ICorporateWebLinkBuilder, CorporateWebLinkBuilder>();

        // Company admin SSO (PR5)
        services.AddScoped<ICompanySsoAdminStore, CompanySsoAdminStore>();
        services.AddScoped<ISsoSecretCipher, SsoSecretCipher>();
        services.AddSingleton<IDnsTxtLookup, DnsTxtLookup>();
        services.AddSingleton<ICorporateSsoPublicSettings, CorporateSsoPublicSettings>();
        services.AddHttpClient(OidcDiscoveryProbe.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "ParkEase-CorporateSso-Admin/1.0");
        });
        services.AddScoped<IOidcDiscoveryProbe, OidcDiscoveryProbe>();

        services.Configure<WaitlistAutoPromotionOptions>(
            configuration.GetSection(WaitlistAutoPromotionOptions.SectionName));
        services.AddScoped<IWaitlistPromotionStore, WaitlistPromotionStore>();
        services.AddHostedService<WaitlistAutoPromotionBackgroundService>();

        return services;
    }
}

