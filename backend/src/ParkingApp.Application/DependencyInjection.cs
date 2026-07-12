using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Application.Validators;

namespace ParkingApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<RegisterDto>, RegisterDtoValidator>();
        services.AddScoped<IValidator<LoginDto>, LoginDtoValidator>();
        services.AddScoped<IValidator<CreateParkingSpaceDto>, CreateParkingSpaceDtoValidator>();
        services.AddScoped<IValidator<CreateBookingDto>, CreateBookingDtoValidator>();
        services.AddScoped<IValidator<CreateBookingCommand>, CreateBookingCommandValidator>();
        services.AddScoped<IValidator<CreateReviewDto>, CreateReviewDtoValidator>();
        services.AddScoped<IValidator<CreateParkingPassDto>, CreateParkingPassDtoValidator>();
        services.AddScoped<IValidator<AssignCorporatePassDto>, AssignCorporatePassDtoValidator>();

        services.AddScoped<IParkingAvailabilityPredictionService, ParkingAvailabilityPredictionService>();
        services.AddScoped<IParkingPassPricingService, ParkingPassPricingService>();
        services.AddScoped<IBookingAvailabilityService, BookingAvailabilityService>();
        services.AddScoped<IWaitlistPromotionService, WaitlistPromotionService>();
        services.AddScoped<ICorporateInvoiceCalculator, CorporateInvoiceCalculator>();

        // Scans command/query/domain-event handlers + pipeline behaviors (PR-16)
        services.AddCQRS(throwIfMissingHandlers: false);

        return services;
    }
}
