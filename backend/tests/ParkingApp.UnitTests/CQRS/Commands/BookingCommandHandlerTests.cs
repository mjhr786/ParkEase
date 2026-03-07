using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Handlers.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class BookingCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationCoordinator> _mockNotification;
    private readonly Mock<IEmailService> _mockEmail;
    private readonly Mock<ICacheService> _mockCache;

    public BookingCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockUserRepo = new Mock<IUserRepository>();

        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);

        _mockNotification = new Mock<INotificationCoordinator>();
        _mockEmail = new Mock<IEmailService>();
        _mockCache = new Mock<ICacheService>();
    }

    // CreateBookingHandler Tests
    [Fact]
    public async Task CreateBookingHandler_ShouldFail_WhenParkingNotAvailable()
    {
        var handler = new CreateBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);

        var res = await handler.HandleAsync(new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), PricingType.Hourly, VehicleType.Car, null, "AB12CD", "Model", "Red", null));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task CreateBookingHandler_ShouldFail_WhenDatesInvalid()
    {
        var handler = new CreateBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), IsActive = true };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(2), PricingType.Hourly, VehicleType.Car, null, null, null, null, null));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("future");
    }

    [Fact]
    public async Task CreateBookingHandler_ShouldSucceed()
    {
        var handler = new CreateBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid(), IsActive = true, HourlyRate = 10, TotalSpots = 1, Owner = new User { Email = "owner@test.com" } };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepo.Setup(r => r.HasOverlappingBookingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Booking { Id = Guid.NewGuid(), User = new User { Email = "user@test.com" } });

        var res = await handler.HandleAsync(new CreateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), PricingType.Hourly, VehicleType.Car, null, "", "", "", null));

        res.Success.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockNotification.Verify(n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // CancelBookingHandler Tests
    [Fact]
    public async Task CancelBookingHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new CancelBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var booking = new Booking { UserId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CancelBookingCommand(Guid.NewGuid(), Guid.NewGuid(), "R"));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("only cancel your own");
    }

    [Fact]
    public async Task CancelBookingHandler_ShouldSucceed()
    {
        var handler = new CancelBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.Pending };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CancelBookingCommand(booking.Id, userId, "Reason"));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ApproveBookingHandler Tests
    [Fact]
    public async Task ApproveBookingHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new ApproveBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var booking = new Booking { ParkingSpace = new ParkingSpace { OwnerId = Guid.NewGuid() } };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new ApproveBookingCommand(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task ApproveBookingHandler_ShouldSucceed()
    {
        var handler = new ApproveBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var vendorId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending, ParkingSpace = new ParkingSpace { OwnerId = vendorId } };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new ApproveBookingCommand(booking.Id, vendorId));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // RejectBookingHandler Tests
    [Fact]
    public async Task RejectBookingHandler_ShouldSucceed()
    {
        var handler = new RejectBookingHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var vendorId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending, ParkingSpace = new ParkingSpace { OwnerId = vendorId } };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new RejectBookingCommand(booking.Id, vendorId, "R"));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Rejected);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // CheckInHandler Tests
    [Fact]
    public async Task CheckInHandler_ShouldSucceed()
    {
        var handler = new CheckInHandler(_mockUow.Object, _mockNotification.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.Confirmed, StartDateTime = DateTime.UtcNow.AddMinutes(-1) };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CheckInCommand(booking.Id, userId));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // CheckOutHandler Tests
    [Fact]
    public async Task CheckOutHandler_ShouldSucceed()
    {
        var handler = new CheckOutHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.InProgress, StartDateTime = DateTime.UtcNow.AddHours(-1) };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CheckOutCommand(booking.Id, userId));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UpdateBookingHandler Tests
    [Fact]
    public async Task UpdateBookingHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new UpdateBookingHandler(_mockUow.Object);
        var booking = new Booking { UserId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new UpdateBookingCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateBookingDto(null, null, VehicleType.Car, "AB", "M")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task UpdateBookingHandler_ShouldSucceed()
    {
        var handler = new UpdateBookingHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.Pending };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new UpdateBookingCommand(booking.Id, userId, new UpdateBookingDto(null, null, null, "NEW123", null)));

        res.Success.Should().BeTrue();
        booking.VehicleNumber.Should().Be("NEW123");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // RequestExtensionHandler Tests
    [Fact]
    public async Task RequestExtensionHandler_ShouldFail_WhenBookingNotFound()
    {
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var handler = new RequestExtensionHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var res = await handler.HandleAsync(new RequestExtensionCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(5)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task RequestExtensionHandler_ShouldFail_WhenUnauthorized()
    {
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.Confirmed };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new RequestExtensionHandler(_mockUow.Object, _mockNotification.Object, _mockEmail.Object, _mockCache.Object);
        var res = await handler.HandleAsync(new RequestExtensionCommand(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(5)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }
}
