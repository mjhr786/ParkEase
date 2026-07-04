using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.Interfaces;
using ParkingApp.Notifications;
using ParkingApp.Notifications.Services;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class DependencyInjectionTests
{
    [Fact]
    public void AddNotificationServices_RegistersExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSignalR();
        services.AddLogging();

        // Build an empty configuration (no Firebase credentials — triggers graceful fallback)
        var config = new ConfigurationBuilder().Build();

        // Act
        services.AddNotificationServices(config);

        // Assert - check service descriptors without resolving (avoids missing IUnitOfWork etc.)
        services.Should().Contain(d => d.ServiceType == typeof(INotificationService)
                                       && d.ImplementationType == typeof(SignalRNotificationService)
                                       && d.Lifetime == ServiceLifetime.Scoped);

        services.Should().Contain(d => d.ServiceType == typeof(ISmsNotificationService)
                                       && d.ImplementationType == typeof(MockSmsNotificationService)
                                       && d.Lifetime == ServiceLifetime.Scoped);

        services.Should().Contain(d => d.ServiceType == typeof(IPushNotificationService)
                                       && d.ImplementationType == typeof(FirebasePushNotificationService)
                                       && d.Lifetime == ServiceLifetime.Scoped);

        services.Should().Contain(d => d.ServiceType == typeof(INotificationCoordinator)
                                       && d.ImplementationType == typeof(NotificationCoordinator)
                                       && d.Lifetime == ServiceLifetime.Scoped);
    }
}
