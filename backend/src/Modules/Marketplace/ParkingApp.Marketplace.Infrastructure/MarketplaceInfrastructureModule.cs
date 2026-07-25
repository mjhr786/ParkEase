using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Infrastructure.ModuleAdapters;
using ParkingApp.Marketplace.Infrastructure.Repositories;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Parking;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Bookings;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Reviews;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Dashboard;
using ParkingApp.Marketplace.Infrastructure.Services;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;

namespace ParkingApp.Marketplace.Infrastructure;

/// <summary>
/// Marketplace module infrastructure: repos, read models, payment, routing, ML, contracts.
/// Host must register <c>IMarketplaceDbContext</c> and <c>IMarketplaceUnitOfWork</c> facades.
/// </summary>
public static class MarketplaceInfrastructureModule
{
    public static IServiceCollection AddMarketplaceInfrastructure(this IServiceCollection services)
        => AddMarketplaceInfrastructure(services, configuration: null);

    public static IServiceCollection AddMarketplaceInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        if (configuration is not null)
        {
            services.Configure<MarketplaceDiscoveryOptions>(
                configuration.GetSection(MarketplaceDiscoveryOptions.SectionName));
            // Also bind Forecast here so Infrastructure ML service sees the same options
            // even if Application registration is delayed or tests only load Infrastructure.
            services.Configure<ForecastOptions>(
                configuration.GetSection(ForecastOptions.SectionName));
            services.Configure<RoutingOptions>(
                configuration.GetSection(RoutingOptions.SectionName));
        }
        else
        {
            services.Configure<MarketplaceDiscoveryOptions>(_ => { });
            services.Configure<ForecastOptions>(_ => { });
            services.Configure<RoutingOptions>(_ => { });
        }

        services.AddScoped<IParkingSpaceRepository, ParkingSpaceRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        // FavoriteRepository / ParkingPassRepository constructed via UnitOfWork.

        services.AddScoped<IParkingReadStore, ParkingReadStore>();
        services.AddScoped<IBookingReadStore, BookingReadStore>();
        services.AddScoped<IReviewReadStore, ReviewReadStore>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddHttpClient<IRoutingService, OSRMService>();
        services.AddScoped<IParkingAvailabilityModelService, ParkingAvailabilityMlModelService>();

        services.AddScoped<IParkingSpaceLookup, ParkingSpaceLookup>();
        services.AddScoped<IBookingLookup, BookingLookup>();
        services.AddScoped<MarketplaceBookingService>();
        services.AddScoped<IMarketplaceBookingService>(sp => sp.GetRequiredService<MarketplaceBookingService>());
        services.AddScoped<IMarketplaceBookingPersistence>(sp => sp.GetRequiredService<MarketplaceBookingService>());
        services.AddScoped<IMarketplaceUserDataCleanup, MarketplaceUserDataCleanup>();

        return services;
    }
}
