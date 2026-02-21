using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Services;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class ServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<INotificationService> _mockNotification;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<BookingService>> _mockLogger;
    private readonly Mock<IEmailService> _mockEmail;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;

    public ServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotification = new Mock<INotificationService>();
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<BookingService>>();
        _mockEmail = new Mock<IEmailService>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
    }

    [Fact]
    public async Task BookingService_CalculatePrice_WithDiscountCode_ShouldApplyDiscount()
    {
        // Arrange
        var service = new BookingService(_mockUnitOfWork.Object, _mockNotification.Object, _mockCache.Object, _mockLogger.Object, _mockEmail.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, HourlyRate = 100 };
        
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        var dto = new PriceCalculationDto(parkingId, start, end, PricingType.Hourly, "SAVE50");

        // Act
        var result = await service.CalculatePriceAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        // Base = 100, Tax = 18, Fee = 5, Discount = 50
        // Total = 100 + 18 + 5 - 50 = 73
        result.Data!.DiscountAmount.Should().Be(50);
        result.Data.TotalAmount.Should().Be(73);
    }

    [Fact]
    public async Task BookingService_Create_WhenSpotsNotAvailable_ShouldReturnFailure()
    {
        // Arrange
        var service = new BookingService(_mockUnitOfWork.Object, _mockNotification.Object, _mockCache.Object, _mockLogger.Object, _mockEmail.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, IsActive = true, TotalSpots = 1 };
        
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        
        var mockBookingRepo = new Mock<IBookingRepository>();
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(mockBookingRepo.Object);
        
        mockBookingRepo.Setup(r => r.HasOverlappingBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockBookingRepo.Setup(r => r.GetActiveBookingsCountAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Already 1 spot taken

        var dto = new CreateBookingDto(parkingId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), PricingType.Hourly, VehicleType.Car, "XYZ", "Model", null);

        // Act
        var result = await service.CreateAsync(Guid.NewGuid(), dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("No spots available for the selected time");
    }
}
