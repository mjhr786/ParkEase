using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Application.Validators;

namespace ParkingApp.Marketplace.Application;

/// <summary>
/// Marketplace module application registration. Call after <c>AddApplication</c>.
/// </summary>
public static class MarketplaceApplicationModule
{
    public static IServiceCollection AddMarketplaceApplication(this IServiceCollection services)
        => AddMarketplaceApplication(services, configuration: null);

    public static IServiceCollection AddMarketplaceApplication(
        this IServiceCollection services,
        IConfiguration? configuration)
    {
        if (configuration is not null)
        {
            services.Configure<ForecastOptions>(configuration.GetSection(ForecastOptions.SectionName));
            services.Configure<RoutingOptions>(configuration.GetSection(RoutingOptions.SectionName));
        }
        else
        {
            services.Configure<ForecastOptions>(_ => { });
            // UseOsrmOnSearch defaults to true — no behavior change when config is absent.
            services.Configure<RoutingOptions>(_ => { });
        }

        services.AddScoped<IValidator<CreateParkingSpaceDto>, CreateParkingSpaceDtoValidator>();
        services.AddScoped<IValidator<CreateBookingDto>, CreateBookingDtoValidator>();
        services.AddScoped<IValidator<CreateBookingCommand>, CreateBookingCommandValidator>();
        services.AddScoped<IValidator<CreateReviewDto>, CreateReviewDtoValidator>();
        services.AddScoped<IValidator<CreateParkingPassDto>, CreateParkingPassDtoValidator>();

        services.AddScoped<IParkingAvailabilityPredictionService, ParkingAvailabilityPredictionService>();
        services.AddScoped<IParkingPassPricingService, ParkingPassPricingService>();
        services.AddScoped<IBookingAvailabilityService, BookingAvailabilityService>();

        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
