using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ParkingApp.Application;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Behaviors;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.CQRS.Queries.Corporate;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS;

public class HandlerRegistrationTests
{
    private static ServiceProvider BuildApplicationProvider()
    {
        var services = new ServiceCollection();
        var uow = new Mock<IUnitOfWork>();
        // Context ports share the same UoW mock (matches Infrastructure DI)
        services.AddScoped(_ => uow.Object);
        services.AddScoped<IMarketplaceUnitOfWork>(_ => uow.Object);
        services.AddScoped<IIdentityUnitOfWork>(_ => uow.Object);
        services.AddScoped<IMessagingUnitOfWork>(_ => uow.Object);
        services.AddScoped<ICorporateUnitOfWork>(_ => uow.Object);
        services.AddScoped(_ => new Mock<ICacheService>().Object);
        services.AddScoped(_ => new Mock<IEmailService>().Object);
        services.AddScoped(_ => new Mock<ISqlConnectionFactory>().Object);
        services.AddScoped(_ => new Mock<ITokenService>().Object);
        services.AddScoped(_ => new Mock<INotificationCoordinator>().Object);
        services.AddScoped(_ => new Mock<IPaymentService>().Object);
        services.AddScoped(_ => new Mock<INotificationService>().Object);
        services.AddScoped(_ => new Mock<IParkingAvailabilityModelService>().Object);
        services.AddScoped(_ => new Mock<IPasswordHasher>().Object);
        services.AddScoped(_ => new Mock<IFileStorage>().Object);
        services.AddScoped(_ => new Mock<IRoutingService>().Object);
        services.AddScoped(_ => new Mock<ICompanyReadStore>().Object);
        services.AddScoped(_ => new Mock<IParkingReadStore>().Object);
        services.AddScoped(_ => new Mock<IBookingReadStore>().Object);
        services.AddScoped(_ => new Mock<IReviewReadStore>().Object);
        services.AddScoped(_ => new Mock<ICompanyQuotaCache>().Object);
        services.AddScoped(_ => new Mock<IDashboardRepository>().Object);
        services.AddScoped(_ => new Mock<IWaitlistPromotionStore>().Object);
        services.AddSingleton(_ => new Mock<ICorporateWebLinkBuilder>().Object);
        services.AddLogging();
        services.AddApplication();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddCQRS_RegistersHandlersAndBehaviors_WithNoMissingRequests()
    {
        var services = new ServiceCollection();
        services.AddCQRS(throwIfMissingHandlers: true);

        var result = services.BuildServiceProvider().GetRequiredService<HandlerRegistrationResult>();

        result.CommandHandlers.Should().BeGreaterThan(40);
        result.QueryHandlers.Should().BeGreaterThan(20);
        result.DomainEventHandlers.Should().BeGreaterThanOrEqualTo(9);
        result.Behaviors.Should().Be(3);
        result.IsComplete.Should().BeTrue();
        result.MissingCommandHandlers.Should().BeEmpty();
        result.MissingQueryHandlers.Should().BeEmpty();
    }

    [Fact]
    public void AddApplication_ResolvesSampleCommandAndQueryHandlers()
    {
        using var sp = BuildApplicationProvider();

        sp.GetService<ICommandHandler<RegisterCommand, ApiResponse<TokenDto>>>().Should().NotBeNull();
        sp.GetService<ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>>().Should().NotBeNull();
        sp.GetService<IQueryHandler<GetUserBookingsQuery, ApiResponse<BookingListResultDto>>>().Should().NotBeNull();
        sp.GetService<IQueryHandler<SearchParkingQuery, ApiResponse<ParkingSearchResultDto>>>().Should().NotBeNull();
        sp.GetService<IQueryHandler<GetMyCompaniesQuery, ApiResponse<List<CompanyDto>>>>().Should().NotBeNull();
    }

    [Fact]
    public void AddApplication_RegistersMultipleDomainEventHandlers_ForSameEvent()
    {
        using var sp = BuildApplicationProvider();

        var handlers = sp.GetServices<IDomainEventHandler<BookingCancelledEvent>>().ToList();
        handlers.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AddApplication_RegistersDispatcherBehaviors()
    {
        using var sp = BuildApplicationProvider();

        var behaviors = sp.GetServices<IDispatcherBehavior>().ToList();
        behaviors.Should().HaveCount(3);
        behaviors.Select(b => b.GetType().Name).Should().Contain(new[]
        {
            nameof(LoggingBehavior),
            nameof(ValidationBehavior),
            nameof(TransactionBehavior)
        });
    }
}
