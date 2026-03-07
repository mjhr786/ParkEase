using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.UnitTests.Bookings;

public class BookingActionHandlersTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<INotificationCoordinator> _mockNotificationCoordinator;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ICacheService> _mockCacheService;

    public BookingActionHandlersTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockNotificationCoordinator = new Mock<INotificationCoordinator>();
        _mockEmailService = new Mock<IEmailService>();
        _mockCacheService = new Mock<ICacheService>();

        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
    }

    [Fact]
    public async Task CancelBookingHandler_WhenAuthorized_ShouldCancelSuccessfully()
    {
        // Arrange
        var handler = new CancelBookingHandler(_mockUnitOfWork.Object, _mockNotificationCoordinator.Object, _mockEmailService.Object, _mockCacheService.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = BookingStatus.Pending,
            BookingReference = "REF123",
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Space" }
        };

        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new CancelBookingCommand(booking.Id, userId, "No longer needed");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("No longer needed");
        _mockNotificationCoordinator.Verify(n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveBookingHandler_WhenVendor_ShouldSetToAwaitingPayment()
    {
        // Arrange
        var handler = new ApproveBookingHandler(_mockUnitOfWork.Object, _mockNotificationCoordinator.Object, _mockEmailService.Object, _mockCacheService.Object);
        var vendorId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            Status = BookingStatus.Pending,
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId, Title = "Space" }
        };

        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new ApproveBookingCommand(booking.Id, vendorId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
    }

    [Fact]
    public async Task RejectBookingHandler_WhenVendor_ShouldRejectSuccessfully()
    {
        // Arrange
        var handler = new RejectBookingHandler(_mockUnitOfWork.Object, _mockNotificationCoordinator.Object, _mockEmailService.Object, _mockCacheService.Object);
        var vendorId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            Status = BookingStatus.Pending,
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId, Title = "Space" }
        };

        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new RejectBookingCommand(booking.Id, vendorId, "Sold out");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Rejected); // Reject sets status to Rejected in domain logic
        booking.CancellationReason.Should().Be("Sold out");
    }

    [Fact]
    public async Task CheckInHandler_WhenUser_ShouldSetToInProgress()
    {
        // Arrange
        var handler = new CheckInHandler(_mockUnitOfWork.Object, _mockNotificationCoordinator.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = BookingStatus.Confirmed,
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddHours(2),
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Space" }
        };

        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new CheckInCommand(booking.Id, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);
        booking.CheckInTime.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckOutHandler_WhenUser_ShouldSetToCompleted()
    {
        // Arrange
        var handler = new CheckOutHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = BookingStatus.InProgress,
            ParkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Space" }
        };

        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var command = new CheckOutCommand(booking.Id, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckOutTime.Should().NotBeNull();
    }
}
