using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Queries.Payments;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Queries;

public class PaymentQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IPaymentRepository> _mockPaymentRepo;
    private readonly Mock<ILogger<GetPaymentByIdHandler>> _mockLoggerId;
    private readonly Mock<ILogger<GetPaymentByBookingIdHandler>> _mockLoggerBooking;

    public PaymentQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockPaymentRepo = new Mock<IPaymentRepository>();
        _mockUow.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);

        _mockLoggerId = new Mock<ILogger<GetPaymentByIdHandler>>();
        _mockLoggerBooking = new Mock<ILogger<GetPaymentByBookingIdHandler>>();
    }

    // GetPaymentByIdHandler Tests
    [Fact]
    public async Task GetPaymentByIdHandler_ShouldFail_WhenNotFound()
    {
        var handler = new GetPaymentByIdHandler(_mockUow.Object, _mockLoggerId.Object);
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment)null);

        var res = await handler.HandleAsync(new GetPaymentByIdQuery(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetPaymentByIdHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new GetPaymentByIdHandler(_mockUow.Object, _mockLoggerId.Object);
        var payment = new Payment { Id = Guid.NewGuid(), Booking = new Booking { UserId = Guid.NewGuid() } };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new GetPaymentByIdQuery(payment.Id, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task GetPaymentByIdHandler_ShouldSucceed()
    {
        var handler = new GetPaymentByIdHandler(_mockUow.Object, _mockLoggerId.Object);
        var userId = Guid.NewGuid();
        var payment = new Payment { Id = Guid.NewGuid(), Booking = new Booking { UserId = userId }, Amount = 50 };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new GetPaymentByIdQuery(payment.Id, userId));

        res.Success.Should().BeTrue();
        res.Data.Amount.Should().Be(50);
    }

    // GetPaymentByBookingIdHandler Tests
    [Fact]
    public async Task GetPaymentByBookingIdHandler_ShouldFail_WhenNotFound()
    {
        var handler = new GetPaymentByBookingIdHandler(_mockUow.Object, _mockLoggerBooking.Object);
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment)null);

        var res = await handler.HandleAsync(new GetPaymentByBookingIdQuery(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetPaymentByBookingIdHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new GetPaymentByBookingIdHandler(_mockUow.Object, _mockLoggerBooking.Object);
        var bookingId = Guid.NewGuid();
        var payment = new Payment { BookingId = bookingId, UserId = Guid.NewGuid() };
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new GetPaymentByBookingIdQuery(bookingId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task GetPaymentByBookingIdHandler_ShouldSucceed()
    {
        var handler = new GetPaymentByBookingIdHandler(_mockUow.Object, _mockLoggerBooking.Object);
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var payment = new Payment { BookingId = bookingId, UserId = userId, Amount = 100 };
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(bookingId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new GetPaymentByBookingIdQuery(bookingId, userId));

        res.Success.Should().BeTrue();
        res.Data.Amount.Should().Be(100);
    }
}
