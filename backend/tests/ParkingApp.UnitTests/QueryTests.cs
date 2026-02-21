using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using System.Linq.Expressions;

namespace ParkingApp.UnitTests;

public class QueryTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<GetParkingByIdHandler>> _mockGetByIdLogger;
    private readonly Mock<ILogger<SearchParkingHandler>> _mockSearchLogger;

    public QueryTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockCache = new Mock<ICacheService>();
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
    public async Task SearchParkingHandler_WhenFound_ShouldSucceedWithReservations()
    {
        // Arrange
        var handler = new SearchParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockSearchLogger.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), Title = "Test Park", IsActive = true, Owner = new User { FirstName = "Owner" } };
        var bookings = new List<Booking> { new Booking { ParkingSpaceId = parking.Id, StartDateTime = DateTime.UtcNow.AddHours(1), EndDateTime = DateTime.UtcNow.AddHours(2) } };

        _mockParkingRepository.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpace> { parking });
        _mockParkingRepository.Setup(r => r.CountAsync(It.IsAny<Expression<Func<ParkingSpace, bool>>>(), It.IsAny<CancellationToken>()))
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
}
