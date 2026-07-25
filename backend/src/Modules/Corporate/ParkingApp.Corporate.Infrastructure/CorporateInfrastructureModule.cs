using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Infrastructure.ReadStores;
using ParkingApp.Corporate.Infrastructure.Services;

namespace ParkingApp.Corporate.Infrastructure;

/// <summary>
/// Corporate module infrastructure: company repos, read stores, quota cache, tenant context, waitlist promotion.
/// </summary>
public static class CorporateInfrastructureModule
{
    public static IServiceCollection AddCorporateInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICompanyReadStore, CompanyReadStore>();
        services.AddScoped<ICorporateTenantContext, CorporateTenantContext>();
        services.AddScoped<ICompanyQuotaCache, CompanyQuotaCache>();
        services.AddSingleton<ICorporateWebLinkBuilder, CorporateWebLinkBuilder>();

        services.Configure<WaitlistAutoPromotionOptions>(
            configuration.GetSection(WaitlistAutoPromotionOptions.SectionName));
        services.AddScoped<IWaitlistPromotionStore, WaitlistPromotionStore>();
        services.AddHostedService<WaitlistAutoPromotionBackgroundService>();

        return services;
    }
}

