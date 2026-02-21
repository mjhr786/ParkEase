using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class BookingTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;

    public BookingTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();

        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepository.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepository.Object);
    }

    [Fact]
    public async Task CreateBookingHandler_WhenParkingDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateBookingHandler(_mockUnitOfWork.Object);
        _mockParkingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var command = new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), PricingType.Hourly, VehicleType.Car, "XYZ", "Tesla", null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space is not available");
    }

    [Fact]
    public async Task CreateBookingHandler_WithValidData_ShouldCreateBooking()
    {
        // Arrange
        var handler = new CreateBookingHandler(_mockUnitOfWork.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), IsActive = true, TotalSpots = 5, HourlyRate = 10 };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        
        _mockBookingRepository.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());
        
        _mockBookingRepository.Setup(r => r.HasOverlappingBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(2);
        var command = new CreateBookingCommand(Guid.NewGuid(), parking.Id, start, end, PricingType.Hourly, VehicleType.Car, "XYZ", "Tesla", null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalAmount.Should().BeGreaterThan(0);
        
        _mockBookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelBookingHandler_WhenNotOwner_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CancelBookingHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = Guid.NewGuid() }; // Different user
        
        _mockBookingRepository.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new CancelBookingCommand(booking.Id, userId, "No longer needed");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("You can only cancel your own bookings");
    }

    [Fact]
    public async Task CreateBookingHandler_WhenNoSpotsAvailable_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateBookingHandler(_mockUnitOfWork.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), IsActive = true, TotalSpots = 1 };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        
        _mockBookingRepository.Setup(r => r.HasOverlappingBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        _mockBookingRepository.Setup(r => r.GetActiveBookingsCountAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Already 1 active booking for 1 spot

        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(2);
        var command = new CreateBookingCommand(Guid.NewGuid(), parking.Id, start, end, PricingType.Hourly, VehicleType.Car, "XYZ", "Tesla", null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("No spots available for the selected time");
    }
}
