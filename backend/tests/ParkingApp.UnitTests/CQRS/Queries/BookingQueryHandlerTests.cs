using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.CQRS.Handlers.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Queries;

public class BookingQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;

    public BookingQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
    }

    // GetBookingByIdHandler Tests
    [Fact]
    public async Task GetBookingByIdHandler_ShouldFail_WhenNotFound()
    {
        var handler = new GetBookingByIdHandler(_mockUow.Object);
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking)null);

        var res = await handler.HandleAsync(new GetBookingByIdQuery(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetBookingByIdHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new GetBookingByIdHandler(_mockUow.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), ParkingSpace = new ParkingSpace { OwnerId = Guid.NewGuid() } };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new GetBookingByIdQuery(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task GetBookingByIdHandler_ShouldSucceed()
    {
        var handler = new GetBookingByIdHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, ParkingSpace = new ParkingSpace { OwnerId = Guid.NewGuid() } };
        _mockBookingRepo.Setup(r => r.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new GetBookingByIdQuery(booking.Id, userId));

        res.Success.Should().BeTrue();
        res.Data.Id.Should().Be(booking.Id);
    }

    // GetBookingByReferenceHandler Tests
    [Fact]
    public async Task GetBookingByReferenceHandler_ShouldFail_WhenNotFound()
    {
        var handler = new GetBookingByReferenceHandler(_mockUow.Object);
        _mockBookingRepo.Setup(r => r.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking)null);

        var res = await handler.HandleAsync(new GetBookingByReferenceQuery("REF123"));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetBookingByReferenceHandler_ShouldSucceed()
    {
        var handler = new GetBookingByReferenceHandler(_mockUow.Object);
        var booking = new Booking { BookingReference = "REF123", Id = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByReferenceAsync("REF123", It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new GetBookingByReferenceQuery("REF123"));

        res.Success.Should().BeTrue();
        res.Data.BookingReference.Should().Be("REF123");
    }

    // GetUserBookingsHandler Tests
    [Fact]
    public async Task GetUserBookingsHandler_ShouldSucceed()
    {
        var handler = new GetUserBookingsHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var bookings = new List<Booking> { new Booking { UserId = userId, Status = BookingStatus.Pending } };
        _mockBookingRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);

        var filter = new BookingFilterDto { Status = BookingStatus.Pending };
        var res = await handler.HandleAsync(new GetUserBookingsQuery(userId, filter));

        res.Success.Should().BeTrue();
        res.Data.TotalCount.Should().Be(1);
    }

    // GetVendorBookingsHandler Tests
    [Fact]
    public async Task GetVendorBookingsHandler_ShouldSucceed()
    {
        var handler = new GetVendorBookingsHandler(_mockUow.Object);
        var vendorId = Guid.NewGuid();
        var bookings = new List<Booking> { new Booking { ParkingSpace = new ParkingSpace { OwnerId = vendorId } } };
        _mockBookingRepo.Setup(r => r.GetByVendorIdAsync(vendorId, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);

        var res = await handler.HandleAsync(new GetVendorBookingsQuery(vendorId, null));

        res.Success.Should().BeTrue();
        res.Data.TotalCount.Should().Be(1);
    }

    // CalculatePriceHandler Tests
    [Fact]
    public async Task CalculatePriceHandler_ShouldFail_WhenParkingNotFound()
    {
        var handler = new CalculatePriceHandler(_mockUow.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);

        var res = await handler.HandleAsync(new CalculatePriceQuery(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(2), 2, null));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CalculatePriceHandler_ShouldSucceed()
    {
        var handler = new CalculatePriceHandler(_mockUow.Object);
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, HourlyRate = 10 };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var now = DateTime.UtcNow;
        var res = await handler.HandleAsync(new CalculatePriceQuery(parking.Id, now, now.AddHours(2), 0, null));

        res.Success.Should().BeTrue();
        res.Data.BaseAmount.Should().Be(20);
    }

    // GetPendingRequestsCountHandler Tests
    [Fact]
    public async Task GetPendingRequestsCountHandler_ShouldSucceed()
    {
        var handler = new GetPendingRequestsCountHandler(_mockUow.Object);
        var vendorId = Guid.NewGuid();
        var parkingId1 = Guid.NewGuid();
        var parkingSpaces = new List<ParkingSpace> { new ParkingSpace { Id = parkingId1 } };
        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(vendorId, It.IsAny<CancellationToken>())).ReturnsAsync(parkingSpaces);

        var bookings = new List<Booking> { new Booking { Status = BookingStatus.Pending }, new Booking { Status = BookingStatus.Confirmed } };
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(parkingId1, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);

        var res = await handler.HandleAsync(new GetPendingRequestsCountQuery(vendorId));

        res.Success.Should().BeTrue();
        res.Data!.Should().Be(1);
    }

    // GetBookingsByParkingSpaceHandler Tests
    [Fact]
    public async Task GetBookingsByParkingSpaceHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new GetBookingsByParkingSpaceHandler(_mockUow.Object);
        var parkingSpaceId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingSpaceId, OwnerId = Guid.NewGuid() };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new GetBookingsByParkingSpaceQuery(parkingSpaceId, Guid.NewGuid(), null));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task GetBookingsByParkingSpaceHandler_ShouldSucceed()
    {
        var handler = new GetBookingsByParkingSpaceHandler(_mockUow.Object);
        var vendorId = Guid.NewGuid();
        var parkingSpaceId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingSpaceId, OwnerId = vendorId };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        var bookings = new List<Booking> { new Booking { Status = BookingStatus.Confirmed } };
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(parkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);

        var res = await handler.HandleAsync(new GetBookingsByParkingSpaceQuery(parkingSpaceId, vendorId, new BookingFilterDto { Status = BookingStatus.Confirmed }));

        res.Success.Should().BeTrue();
        res.Data.TotalCount.Should().Be(1);
    }
}
