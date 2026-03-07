using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.Dashboard;

public class DashboardHandlerTests
{
    private readonly Mock<IDashboardRepository> _mockRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<GetVendorDashboardHandler>> _mockVendorLogger;

    public DashboardHandlerTests()
    {
        _mockRepo = new Mock<IDashboardRepository>();
        _mockCache = new Mock<ICacheService>();
        _mockVendorLogger = new Mock<ILogger<GetVendorDashboardHandler>>();
    }

    [Fact]
    public async Task GetVendorDashboardHandler_WhenNotCached_ShouldQueryDbAndCache()
    {
        // Arrange
        var handler = new GetVendorDashboardHandler(_mockRepo.Object, _mockCache.Object, _mockVendorLogger.Object);
        var vendorId = Guid.NewGuid();
        var query = new GetVendorDashboardQuery(vendorId);

        _mockCache.Setup(c => c.GetAsync<VendorDashboardDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorDashboardDto?)null);

        // Mock aggregate row
        var aggregateRow = new VendorAggregateRow
        { 
            TotalParkingSpaces = 2, 
            ActiveParkingSpaces = 1, 
            AverageRating = 4.5, 
            TotalReviews = 10,
            TotalBookings = 20,
            ActiveBookings = 2,
            PendingBookings = 1,
            CompletedBookings = 17,
            TotalEarnings = 5000m,
            MonthlyEarnings = 1000m,
            WeeklyEarnings = 200m
        };

        // Mock chart data
        var chartData = new List<DashboardChartDataDto> { new DashboardChartDataDto("Mon", 100m, 5) };

        // Mock recent bookings
        var recentBookings = new List<BookingDto> { new BookingDto { Id = Guid.NewGuid() } };

        _mockRepo.Setup(r => r.GetVendorAggregatesAsync(vendorId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggregateRow);

        _mockRepo.Setup(r => r.GetChartDataAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chartData);

        _mockRepo.Setup(r => r.GetRecentVendorBookingsAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentBookings);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalParkingSpaces.Should().Be(2);
        
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<VendorDashboardDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberDashboardHandler_WhenNotCached_ShouldQueryDbAndCache()
    {
        // Arrange
        var handler = new GetMemberDashboardHandler(_mockRepo.Object, _mockCache.Object);
        var memberId = Guid.NewGuid();
        var query = new GetMemberDashboardQuery(memberId);

        _mockCache.Setup(c => c.GetAsync<MemberDashboardDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberDashboardDto?)null);

        // Mock aggregate row
        var aggRow = new MemberAggregateRow
        { 
            TotalBookings = 10, 
            ActiveBookings = 2, 
            CompletedBookings = 8, 
            TotalSpent = 1500m 
        };

        // Mock upcoming & recent bookings
        var bookings = new List<BookingDto> { new BookingDto { Id = Guid.NewGuid(), UserId = memberId } };

        _mockRepo.Setup(r => r.GetMemberAggregatesAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aggRow);

        _mockRepo.Setup(r => r.GetUpcomingMemberBookingsAsync(memberId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        _mockRepo.Setup(r => r.GetRecentMemberBookingsAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalBookings.Should().Be(10);
        
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<MemberDashboardDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
