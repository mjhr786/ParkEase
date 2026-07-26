using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Commands.Lpr;
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
            services.Configure<LprAccessOptions>(configuration.GetSection(LprAccessOptions.SectionName));
            services.Configure<SessionReminderOptions>(configuration.GetSection(SessionReminderOptions.SectionName));
            services.Configure<WalletPassOptions>(configuration.GetSection(WalletPassOptions.SectionName));
            services.Configure<ValetOptions>(configuration.GetSection(ValetOptions.SectionName));
        }
        else
        {
            services.Configure<ForecastOptions>(_ => { });
            // UseOsrmOnSearch defaults to true — no behavior change when config is absent.
            services.Configure<RoutingOptions>(_ => { });
            services.Configure<LprAccessOptions>(_ => { });
            services.Configure<SessionReminderOptions>(_ => { });
            services.Configure<WalletPassOptions>(_ => { });
            services.Configure<ValetOptions>(_ => { });
        }

        services.AddScoped<IValidator<CreateParkingSpaceDto>, CreateParkingSpaceDtoValidator>();
        services.AddScoped<IValidator<CreateBookingDto>, CreateBookingDtoValidator>();
        services.AddScoped<IValidator<CreateBookingCommand>, CreateBookingCommandValidator>();
        services.AddScoped<IValidator<CreateReviewDto>, CreateReviewDtoValidator>();
        services.AddScoped<IValidator<CreateParkingPassDto>, CreateParkingPassDtoValidator>();
        services.AddScoped<IValidator<ProcessLprAccessCommand>, ProcessLprAccessCommandValidator>();

        services.AddScoped<IParkingAvailabilityPredictionService, ParkingAvailabilityPredictionService>();
        services.AddScoped<IParkingPassPricingService, ParkingPassPricingService>();
        services.AddScoped<IBookingAvailabilityService, BookingAvailabilityService>();
        services.AddScoped<IOverstayDetectionService, OverstayDetectionService>();
        services.AddScoped<ISessionReminderService, SessionReminderService>();

        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
