using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Infrastructure.Persistence;

namespace ParkingApp.Infrastructure.Modules;

/// <summary>
/// Host bridge: binds shared DbContext/UnitOfWork to Messaging module infrastructure.
/// </summary>
public static class MessagingInfrastructureModule
{
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services)
    {
        // Shared ApplicationDbContext implements the Messaging persistence facade
        services.AddScoped<IMessagingDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Messaging UoW port ΓåÆ shared UnitOfWork (one DbContext transaction)
        services.AddScoped<IMessagingUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());

        // Repositories + IConversationLookup live in ParkingApp.Messaging.Infrastructure
        ParkingApp.Messaging.Infrastructure.MessagingInfrastructureModule.AddMessagingInfrastructure(services);

        return services;
    }
}
