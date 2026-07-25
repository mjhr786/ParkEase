using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;

namespace ParkingApp.Messaging.Application;

/// <summary>
/// Messaging module application registration (composition root calls this after <c>AddApplication</c>).
/// </summary>
public static class MessagingApplicationModule
{
    /// <summary>
    /// Registers Messaging CQRS command/query handlers from this assembly.
    /// Requires the shared dispatcher from <c>AddCQRS</c> / <c>AddApplication</c>.
    /// </summary>
    public static IServiceCollection AddMessagingApplication(this IServiceCollection services)
    {
        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
