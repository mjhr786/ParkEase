using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Application.Services;

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

        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
