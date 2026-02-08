using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.Interfaces;
using ParkingApp.Notifications.Services;

namespace ParkingApp.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        // SignalR Real-time Notifications
        services.AddScoped<Application.Interfaces.INotificationService, SignalRNotificationService>();
        
        // External Notification Services (SMS/Push)
        services.AddScoped<ISmsNotificationService, MockSmsNotificationService>();
        services.AddScoped<IPushNotificationService, MockPushNotificationService>();
        
        // Notification Coordinator (orchestrates all channels)
        services.AddScoped<INotificationCoordinator, NotificationCoordinator>();
        
        return services;
    }
}
