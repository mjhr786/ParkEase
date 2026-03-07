using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class BookingServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<INotificationCoordinator> _mockNotification;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<BookingService>> _mockLogger;
    private readonly Mock<IEmailService> _mockEmail;
    private readonly BookingService _service;

    public BookingServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        
        _mockNotification = new Mock<INotificationCoordinator>();
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<BookingService>>();
        _mockEmail = new Mock<IEmailService>();

        _service = new BookingService(_mockUow.Object, _mockNotification.Object, _mockCache.Object, _mockLogger.Object, _mockEmail.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldFail_WhenNotFound()
    {
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);
        var res = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldFail_WhenUnauthorized()
    {
        var booking = new Booking { UserId = Guid.NewGuid(), ParkingSpace = new ParkingSpace { OwnerId = Guid.NewGuid() } };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());
        res.Success.Should().BeFalse();
        res.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldSucceed_WhenValid()
    {
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = uid, ParkingSpace = new ParkingSpace { OwnerId = Guid.NewGuid() } };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.GetByIdAsync(booking.Id, uid);
        res.Success.Should().BeTrue();
        res.Data!.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task GetByReferenceAsync_ShouldFail_WhenNotFound()
    {
        _mockBookingRepo.Setup(r => r.GetByReferenceAsync("REF", It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);
        var res = await _service.GetByReferenceAsync("REF");
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByReferenceAsync_ShouldSucceed_WhenValid()
    {
        var booking = new Booking { BookingReference = "REF" };
        _mockBookingRepo.Setup(r => r.GetByReferenceAsync("REF", It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.GetByReferenceAsync("REF");
        res.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserAsync_ShouldReturnPagedList()
    {
        var uid = Guid.NewGuid();
        var bookings = new List<Booking> { new Booking { UserId = uid } };
        _mockBookingRepo.Setup(r => r.GetByUserIdAsync(uid, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);
        var res = await _service.GetByUserAsync(uid, null);
        res.Success.Should().BeTrue();
        res.Data!.Bookings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByParkingSpaceAsync_ShouldFail_WhenUnauthorized()
    {
        var pid = Guid.NewGuid();
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(new ParkingSpace { OwnerId = Guid.NewGuid() });
        var res = await _service.GetByParkingSpaceAsync(pid, Guid.NewGuid(), null);
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByParkingSpaceAsync_ShouldSucceed_WhenValid()
    {
        var pid = Guid.NewGuid();
        var oid = Guid.NewGuid();
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(new ParkingSpace { OwnerId = oid });
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking>());
        var res = await _service.GetByParkingSpaceAsync(pid, oid, null);
        res.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CheckInAsync_ShouldFail_WhenIncorrectStatus()
    {
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = uid, Status = BookingStatus.Pending };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.CheckInAsync(id, uid);
        res.Success.Should().BeFalse();
        res.Message.Should().Contain("must be confirmed");
    }

    [Fact]
    public async Task CheckInAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = uid, Status = BookingStatus.Confirmed, StartDateTime = DateTime.UtcNow.AddMinutes(10) };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.CheckInAsync(id, uid);
        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);
    }

    [Fact]
    public async Task CheckOutAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = uid, Status = BookingStatus.InProgress, ParkingSpaceId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.CheckOutAsync(id, uid);
        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
    }

    [Fact]
    public async Task ApproveAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var oid = Guid.NewGuid();
        var booking = new Booking { Id = id, Status = BookingStatus.Pending, ParkingSpace = new ParkingSpace { OwnerId = oid } };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.ApproveAsync(id, oid);
        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
    }

    [Fact]
    public async Task RejectAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var oid = Guid.NewGuid();
        var booking = new Booking { Id = id, Status = BookingStatus.Pending, ParkingSpace = new ParkingSpace { OwnerId = oid } };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.RejectAsync(id, oid, "Sorry");
        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be("Sorry");
    }

    [Fact]
    public async Task CancelAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = uid, Status = BookingStatus.Pending, StartDateTime = DateTime.UtcNow.AddDays(2), Payment = new Payment { Status = PaymentStatus.Completed }, TotalAmount = 100 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var res = await _service.CancelAsync(id, uid, new CancelBookingDto("Changed mind"));
        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.RefundAmount.Should().Be(100);
    }

    [Fact]
    public async Task GetVendorBookingsAsync_ShouldReturnList()
    {
        var oid = Guid.NewGuid();
        var p1 = new ParkingSpace { Id = Guid.NewGuid() };
        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(oid, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ParkingSpace> { p1 });
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(p1.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking>());
        var res = await _service.GetVendorBookingsAsync(oid, null);
        res.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExtendAsync_ShouldSucceed_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var currentEnd = DateTime.UtcNow.AddHours(2);
        var newEnd = DateTime.UtcNow.AddHours(4);
        
        var booking = new Booking 
        { 
            Id = id, 
            UserId = uid, 
            ParkingSpaceId = pid,
            Status = BookingStatus.Confirmed, 
            EndDateTime = currentEnd,
            PricingType = PricingType.Hourly,
            BaseAmount = 100,
            TaxAmount = 18,
            ServiceFee = 5,
            TotalAmount = 123
        };
        
        var parking = new ParkingSpace { Id = pid, HourlyRate = 50, TotalSpots = 10 };
        
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepo.Setup(r => r.HasOverlappingBookingAsync(pid, currentEnd, newEnd, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
        var dto = new ExtendBookingDto(newEnd);

        // Act
        var res = await _service.ExtendAsync(id, uid, dto);

        // Assert
        res.Success.Should().BeTrue();
        booking.EndDateTime.Should().Be(newEnd);
        booking.BaseAmount.Should().BeGreaterThan(100);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtendAsync_ShouldFail_WhenUnauthorized()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var otherUid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = otherUid };
        
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        
        var dto = new ExtendBookingDto(DateTime.UtcNow.AddHours(4));

        // Act
        var res = await _service.ExtendAsync(id, uid, dto);

        // Assert
        res.Success.Should().BeFalse();
        res.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task ExtendAsync_ShouldFail_WhenInvalidStatus()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var booking = new Booking { Id = id, UserId = uid, Status = BookingStatus.Cancelled };
        
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        
        var dto = new ExtendBookingDto(DateTime.UtcNow.AddHours(4));

        // Act
        var res = await _service.ExtendAsync(id, uid, dto);

        // Assert
        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Only confirmed or in-progress");
    }

    [Fact]
    public async Task ExtendAsync_ShouldFail_WhenNoSpotsAvailable()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var currentEnd = DateTime.UtcNow.AddHours(2);
        var newEnd = DateTime.UtcNow.AddHours(4);
        
        var booking = new Booking { Id = id, UserId = uid, ParkingSpaceId = pid, Status = BookingStatus.Confirmed, EndDateTime = currentEnd };
        var parking = new ParkingSpace { Id = pid, TotalSpots = 1 };
        
        _mockBookingRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepo.Setup(r => r.HasOverlappingBookingAsync(pid, currentEnd, newEnd, id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockBookingRepo.Setup(r => r.GetActiveBookingsCountAsync(pid, currentEnd, newEnd, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        
        var dto = new ExtendBookingDto(newEnd);

        // Act
        var res = await _service.ExtendAsync(id, uid, dto);

        // Assert
        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not available");
    }
}
