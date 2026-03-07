using FluentAssertions;
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

        // Act
        services.AddNotificationServices();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<INotificationService>().Should().BeOfType<SignalRNotificationService>();
        provider.GetService<ISmsNotificationService>().Should().BeOfType<MockSmsNotificationService>();
        provider.GetService<IPushNotificationService>().Should().BeOfType<MockPushNotificationService>();
        
        // NotificationCoordinator requires dependencies not mocked here (IUnitOfWork, etc.), so just check the descriptor
        services.Should().Contain(d => d.ServiceType == typeof(INotificationCoordinator) 
                                       && d.ImplementationType == typeof(NotificationCoordinator)
                                       && d.Lifetime == ServiceLifetime.Scoped);
    }
}
