using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Application.Validators;

namespace ParkingApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators
        services.AddScoped<IValidator<RegisterDto>, RegisterDtoValidator>();
        services.AddScoped<IValidator<LoginDto>, LoginDtoValidator>();
        services.AddScoped<IValidator<CreateParkingSpaceDto>, CreateParkingSpaceDtoValidator>();
        services.AddScoped<IValidator<CreateBookingDto>, CreateBookingDtoValidator>();
        services.AddScoped<IValidator<CreateReviewDto>, CreateReviewDtoValidator>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IParkingSpaceService, ParkingSpaceService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IPaymentAppService, PaymentAppService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // CQRS
        services.AddCQRS();

        return services;
    }
}
