using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.CQRS;
using Moq;
using FluentAssertions;
using Xunit;

namespace ParkingApp.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ShouldRegisterExpectedServices()
    {
        var services = new ServiceCollection();
        
        services.AddScoped(x => new Mock<ParkingApp.Domain.Interfaces.IUnitOfWork>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.IEmailService>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.IFileUploadService>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.ISqlConnectionFactory>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Domain.Interfaces.ITokenService>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.INotificationCoordinator>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Domain.Interfaces.IPaymentService>().Object);
        services.AddScoped(x => new Mock<ParkingApp.Application.Interfaces.INotificationService>().Object);
        services.AddLogging();
        services.AddApplication();

        var serviceProvider = services.BuildServiceProvider();

        // Check Services
        serviceProvider.GetService<IAuthService>().Should().NotBeNull();
        serviceProvider.GetService<IUserService>().Should().NotBeNull();
        serviceProvider.GetService<IParkingSpaceService>().Should().NotBeNull();
        serviceProvider.GetService<IBookingService>().Should().NotBeNull();
        serviceProvider.GetService<IPaymentAppService>().Should().NotBeNull();
        serviceProvider.GetService<IReviewService>().Should().NotBeNull();
        serviceProvider.GetService<IDashboardService>().Should().NotBeNull();

        // Check CQRS Dispatcher
        serviceProvider.GetService<IDispatcher>().Should().NotBeNull();
    }
}
