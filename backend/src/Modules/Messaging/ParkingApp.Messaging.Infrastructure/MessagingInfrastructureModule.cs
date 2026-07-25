using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Infrastructure.ModuleAdapters;
using ParkingApp.Messaging.Infrastructure.Persistence;
using ParkingApp.Messaging.Infrastructure.Repositories;

namespace ParkingApp.Messaging.Infrastructure;

/// <summary>
/// Messaging module infrastructure registration.
/// Requires a registered <see cref="IMessagingDbContext"/> implementation (shared ApplicationDbContext)
/// and an <see cref="IMessagingUnitOfWork"/> implementation (shared UnitOfWork).
/// </summary>
public static class MessagingInfrastructureModule
{
    /// <summary>
    /// Registers Messaging repositories and outward contract adapters.
    /// Call after the host registers DbContext + UnitOfWork implementations for the facades.
    /// </summary>
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IConversationLookup, ConversationLookup>();
        services.AddScoped<INotificationInbox, NotificationInbox>();
        services.AddScoped<IMessagingUserDataCleanup, MessagingUserDataCleanup>();
        return services;
    }
}
