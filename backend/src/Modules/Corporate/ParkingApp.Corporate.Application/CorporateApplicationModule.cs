using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Application.Validators;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Corporate.Application;

/// <summary>
/// Corporate module application registration (companies, allocations, corporate bookings, waitlist, invoices).
/// </summary>
public static class CorporateApplicationModule
{
    public static IServiceCollection AddCorporateApplication(this IServiceCollection services)
    {
        services.AddScoped<IWaitlistPromotionService, WaitlistPromotionService>();
        services.AddScoped<ICorporateInvoiceCalculator, CorporateInvoiceCalculator>();

        // Required by PassesController (AssignCorporate) — missing registration broke entire /api/passes/* (DEF-003).
        services.AddScoped<IValidator<AssignCorporatePassDto>, AssignCorporatePassDtoValidator>();

        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
