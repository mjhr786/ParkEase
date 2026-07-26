using ParkingApp.Application.Interfaces;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Marketplace.Application.Queries.Parking;
using ParkingApp.Marketplace.Application.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using System.Linq.Expressions;

namespace ParkingApp.UnitTests;

public class QueryTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<IParkingReadStore> _mockReadStore;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<IRoutingService> _mockRouting;
    private readonly Mock<ILogger<GetParkingByIdHandler>> _mockGetByIdLogger;
    private readonly Mock<ILogger<SearchParkingHandler>> _mockSearchLogger;

    public QueryTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockReadStore = new Mock<IParkingReadStore>();
        _mockCache = new Mock<ICacheService>();
        _mockRouting = new Mock<IRoutingService>();
        _mockGetByIdLogger = new Mock<ILogger<GetParkingByIdHandler>>();
        _mockSearchLogger = new Mock<ILogger<SearchParkingHandler>>();

        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepository.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepository.Object);
    }

    [Fact]
    public async Task GetParkingByIdHandler_WhenCached_ShouldReturnCacheAndSkipDb()
    {
        // Arrange
        var handler = new GetParkingByIdHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockGetByIdLogger.Object);
        var parkingId = Guid.NewGuid();
        var cachedDto = new ParkingSpaceDto(parkingId, Guid.NewGuid(), "Owner", "Title", "Desc", "Addr", "City", "ST", "IN", "123", 12.0, 77.0, ParkingType.Open, 10, 10, 50, 400, 2000, 7000, TimeSpan.FromHours(8), TimeSpan.FromHours(20), true, new List<string>(), new List<VehicleType>(), new List<string>(), true, true, 4.5, 10, null, DateTime.UtcNow);

        _mockCache.Setup(c => c.GetAsync<ParkingSpaceDto>($"parking:{parkingId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await handler.HandleAsync(new GetParkingByIdQuery(parkingId));

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(cachedDto);
        _mockParkingRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculatePriceHandler_ForHourlyPricing_ShouldCalculateCorrectly()
    {
        // Arrange
        var handler = new CalculatePriceHandler(_mockUnitOfWork.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, HourlyRate = 100 };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var start = DateTime.UtcNow;
        var end = start.AddHours(2.5); // Should be rounded up to 3 hours
        var query = new CalculatePriceQuery(parkingId, start, end, (int)PricingType.Hourly, null);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        // 3 hours * 100 = 300 base
        // 300 * 0.18 = 54 tax
        // 300 * 0.05 = 15 fee
        // Total = 369
        result.Data!.BaseAmount.Should().Be(300);
        result.Data.TotalAmount.Should().Be(369);
    }

    [Fact]
    public async Task CalculatePriceHandler_ForDailyPricing_ShouldUseInclusiveCalendarDays_IgnoringClockTimes()
    {
        // Arrange — times differ but both fall on Jul 26 and Jul 28 local (UTC)
        var handler = new CalculatePriceHandler(_mockUnitOfWork.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            HourlyRate = 100,
            DailyRate = 400,
            TimeZoneId = "UTC"
        };

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var start = new DateTime(2026, 7, 26, 17, 23, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 28, 18, 23, 0, DateTimeKind.Utc); // +1 hour vs exact 48h
        var query = new CalculatePriceQuery(parkingId, start, end, (int)PricingType.Daily, null);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert — Jul 26, 27, 28 = 3 full calendar days (clock times ignored)
        result.Success.Should().BeTrue();
        result.Data!.Duration.Should().Be(3);
        result.Data.DurationUnit.Should().Be("days");
        // 3 * 400 = 1200 base; tax 216; fee 60; total 1476
        result.Data.BaseAmount.Should().Be(1200);
        result.Data.TotalAmount.Should().Be(1476);
    }

    [Fact]
    public async Task CalculatePriceHandler_ForDailyPricing_SameCalendarDay_ShouldBillOneDay()
    {
        var handler = new CalculatePriceHandler(_mockUnitOfWork.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            DailyRate = 500,
            TimeZoneId = "UTC"
        };

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var start = new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 26, 18, 30, 0, DateTimeKind.Utc);
        var query = new CalculatePriceQuery(parkingId, start, end, (int)PricingType.Daily, null);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Data!.Duration.Should().Be(1);
        result.Data.DurationUnit.Should().Be("days");
        result.Data.BaseAmount.Should().Be(500);
    }

    [Fact]
    public async Task CalculatePriceHandler_ForWeeklyPricing_ShouldUseCalendarDaysNotHours()
    {
        var handler = new CalculatePriceHandler(_mockUnitOfWork.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            WeeklyRate = 2000,
            TimeZoneId = "UTC"
        };

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        // 8 calendar days (Jul 1–8 inclusive) → 2 weeks
        var start = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 8, 6, 0, 0, DateTimeKind.Utc);
        var query = new CalculatePriceQuery(parkingId, start, end, (int)PricingType.Weekly, null);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Data!.Duration.Should().Be(2);
        result.Data.DurationUnit.Should().Be("weeks");
        result.Data.BaseAmount.Should().Be(4000);
    }

    [Fact]
    public async Task SearchParkingHandler_WhenFound_ShouldSucceedWithReservations()
    {
        // Arrange
        var discovery = Microsoft.Extensions.Options.Options.Create(new ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions());
        var discoveryMonitor = new TestDiscoveryOptionsMonitor(discovery.Value);
        var routingMonitor = new TestRoutingOptionsMonitor(new ParkingApp.Marketplace.Application.Options.RoutingOptions { UseOsrmOnSearch = true });
        var handler = new SearchParkingHandler(
            _mockUnitOfWork.Object, _mockReadStore.Object, _mockCache.Object, _mockRouting.Object,
            discoveryMonitor, routingMonitor, _mockSearchLogger.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), Title = "Test Park", IsActive = true };
        var bookings = new List<Booking> { new Booking { ParkingSpaceId = parking.Id, StartDateTime = DateTime.UtcNow.AddHours(1), EndDateTime = DateTime.UtcNow.AddHours(2) } };

        _mockReadStore.Setup(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpace> { parking });
        _mockReadStore.Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockBookingRepository.Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var searchDto = new ParkingSearchDto { City = "TestCity", Page = 1, PageSize = 10 };

        // Act
        var result = await handler.HandleAsync(new SearchParkingQuery(searchDto));

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ParkingSpaces.Should().HaveCount(1);
        result.Data.ParkingSpaces.First().ActiveReservations.Should().NotBeNull();
    }

    private sealed class TestDiscoveryOptionsMonitor : Microsoft.Extensions.Options.IOptionsMonitor<ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions>
    {
        public TestDiscoveryOptionsMonitor(ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions current) => CurrentValue = current;
        public ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions CurrentValue { get; }
        public ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<ParkingApp.Marketplace.Application.Options.MarketplaceDiscoveryOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }

    private sealed class TestRoutingOptionsMonitor : Microsoft.Extensions.Options.IOptionsMonitor<ParkingApp.Marketplace.Application.Options.RoutingOptions>
    {
        public TestRoutingOptionsMonitor(ParkingApp.Marketplace.Application.Options.RoutingOptions current) => CurrentValue = current;
        public ParkingApp.Marketplace.Application.Options.RoutingOptions CurrentValue { get; }
        public ParkingApp.Marketplace.Application.Options.RoutingOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<ParkingApp.Marketplace.Application.Options.RoutingOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}






