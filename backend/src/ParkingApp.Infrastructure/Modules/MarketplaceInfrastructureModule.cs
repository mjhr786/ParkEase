using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Persistence;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;

namespace ParkingApp.Infrastructure.Modules;

/// <summary>
/// Host bridge for Marketplace: shared DbContext/UoW facades only.
/// Payment, routing, ML, and read models register in module Infrastructure.
/// </summary>
public static class MarketplaceInfrastructureModule
{
    public static IServiceCollection AddMarketplaceInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddScoped<IMarketplaceDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IMarketplaceUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());

        ParkingApp.Marketplace.Infrastructure.MarketplaceInfrastructureModule.AddMarketplaceInfrastructure(
            services,
            configuration);
        return services;
    }
}
