using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Application.CQRS.Handlers.Bookings;

namespace ParkingApp.UnitTests.Bookings;

public class UpdateBookingHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;

    public UpdateBookingHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateBookingHandler(_mockUnitOfWork.Object);
        var command = new UpdateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateBookingDto(null, null, null, null, null));

        _mockBookingRepo.Setup(r => r.GetByIdAsync(command.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
    }

    [Fact]
    public async Task HandleAsync_WhenUnauthorizedUser_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateBookingHandler(_mockUnitOfWork.Object);
        var booking = new Booking { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = BookingStatus.Pending };
        var command = new UpdateBookingCommand(booking.Id, Guid.NewGuid(), new UpdateBookingDto(null, null, null, null, null));

        _mockBookingRepo.Setup(r => r.GetByIdAsync(command.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleAsync_WithValidBasicUpdates_ShouldUpdateAndReturnSuccess()
    {
        // Arrange
        var handler = new UpdateBookingHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = BookingStatus.Confirmed,
            VehicleNumber = "OLD-123",
            VehicleModel = "Old Model",
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid() }
        };
        
        var dto = new UpdateBookingDto(null, null, VehicleType.SUV, "NEW-123", "New Model");
        var command = new UpdateBookingCommand(booking.Id, userId, dto);

        _mockBookingRepo.Setup(r => r.GetByIdAsync(command.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.VehicleNumber.Should().Be("NEW-123");
        booking.VehicleModel.Should().Be("New Model");
        booking.VehicleType.Should().Be(VehicleType.SUV);
        _mockBookingRepo.Verify(r => r.Update(booking), Times.Once);
        _mockUnitOfWork.Verify(w => w.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
