using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.Interfaces;
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

        var uow = new Mock<ParkingApp.Domain.Interfaces.IUnitOfWork>();
        services.AddScoped(_ => uow.Object);
        services.AddScoped<ParkingApp.Domain.Interfaces.IMarketplaceUnitOfWork>(_ => uow.Object);
        services.AddScoped<ParkingApp.Domain.Interfaces.IIdentityUnitOfWork>(_ => uow.Object);
        services.AddScoped<ParkingApp.Domain.Interfaces.IMessagingUnitOfWork>(_ => uow.Object);
        services.AddScoped<ParkingApp.Domain.Interfaces.ICorporateUnitOfWork>(_ => uow.Object);
        services.AddScoped(_ => new Mock<ICacheService>().Object);
        services.AddScoped(_ => new Mock<IEmailService>().Object);
        services.AddScoped(_ => new Mock<ISqlConnectionFactory>().Object);
        services.AddScoped(_ => new Mock<ITokenService>().Object);
        services.AddScoped(_ => new Mock<INotificationCoordinator>().Object);
        services.AddScoped(_ => new Mock<IPaymentService>().Object);
        services.AddScoped(_ => new Mock<INotificationService>().Object);
        services.AddScoped(_ => new Mock<IParkingAvailabilityModelService>().Object);
        services.AddLogging();
        services.AddApplication();

        var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetService<IDispatcher>().Should().NotBeNull();
        serviceProvider.GetService<IParkingPassPricingService>().Should().NotBeNull();

        var registration = serviceProvider.GetService<HandlerRegistrationResult>();
        registration.Should().NotBeNull();
        registration!.IsComplete.Should().BeTrue();
        registration.TotalHandlers.Should().BeGreaterThan(50);
    }
}