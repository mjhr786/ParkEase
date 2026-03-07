using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Payments;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class PaymentCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IPaymentRepository> _mockPaymentRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<INotificationCoordinator> _mockNotification;
    private readonly Mock<IEmailService> _mockEmail;
    
    private readonly Mock<ILogger<ProcessPaymentHandler>> _mockProcessLogger;
    private readonly Mock<ILogger<CreatePaymentOrderHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<VerifyPaymentHandler>> _mockVerifyLogger;
    private readonly Mock<ILogger<ProcessRefundHandler>> _mockRefundLogger;

    public PaymentCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockPaymentRepo = new Mock<IPaymentRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);

        _mockPaymentService = new Mock<IPaymentService>();
        _mockNotification = new Mock<INotificationCoordinator>();
        _mockEmail = new Mock<IEmailService>();

        _mockProcessLogger = new Mock<ILogger<ProcessPaymentHandler>>();
        _mockCreateLogger = new Mock<ILogger<CreatePaymentOrderHandler>>();
        _mockVerifyLogger = new Mock<ILogger<VerifyPaymentHandler>>();
        _mockRefundLogger = new Mock<ILogger<ProcessRefundHandler>>();
    }

    // ProcessPaymentHandler Tests
    [Fact]
    public async Task ProcessPaymentHandler_ShouldFail_WhenBookingNotFound()
    {
        var handler = new ProcessPaymentHandler(_mockUow.Object, _mockPaymentService.Object, _mockNotification.Object, _mockEmail.Object, _mockProcessLogger.Object);
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking)null);
        
        var res = await handler.HandleAsync(new ProcessPaymentCommand(Guid.NewGuid(), new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard)));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentHandler_ShouldSucceed()
    {
        var handler = new ProcessPaymentHandler(_mockUow.Object, _mockPaymentService.Object, _mockNotification.Object, _mockEmail.Object, _mockProcessLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50, ParkingSpaceId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        
        var paymentResult = new PaymentResult { Success = true, Status = PaymentStatus.Completed, TransactionId = "TXN" };
        _mockPaymentService.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(paymentResult);

        var res = await handler.HandleAsync(new ProcessPaymentCommand(booking.UserId, new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard)));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // CreatePaymentOrderHandler Tests
    [Fact]
    public async Task CreatePaymentOrderHandler_ShouldFail_WhenBookingWrongStatus()
    {
        var handler = new CreatePaymentOrderHandler(_mockUow.Object, _mockPaymentService.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.Pending };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not awaiting payment");
    }

    [Fact]
    public async Task CreatePaymentOrderHandler_ShouldSucceed()
    {
        var handler = new CreatePaymentOrderHandler(_mockUow.Object, _mockPaymentService.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentService.Setup(p => p.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>())).ReturnsAsync("ORDER-123");

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, Guid.NewGuid()));

        res.Success.Should().BeTrue();
        res.Data.Should().Be("ORDER-123");
    }

    // VerifyPaymentHandler Tests
    [Fact]
    public async Task VerifyPaymentHandler_ShouldFail_WhenInvalidSignature()
    {
        var handler = new VerifyPaymentHandler(_mockUow.Object, _mockPaymentService.Object, _mockNotification.Object, _mockEmail.Object, _mockVerifyLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(booking.UserId, new VerifyPaymentDto { BookingId = Guid.NewGuid(), RazorpayPaymentId = "p", RazorpayOrderId = "o", RazorpaySignature = "s" }));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPaymentHandler_ShouldSucceed()
    {
        var handler = new VerifyPaymentHandler(_mockUow.Object, _mockPaymentService.Object, _mockNotification.Object, _mockEmail.Object, _mockVerifyLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50, ParkingSpaceId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(booking.UserId, new VerifyPaymentDto { BookingId = Guid.NewGuid(), RazorpayPaymentId = "p", RazorpayOrderId = "o", RazorpaySignature = "s" }));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ProcessRefundHandler Tests
    [Fact]
    public async Task ProcessRefundHandler_ShouldFail_WhenPaymentNotCompleted()
    {
        var handler = new ProcessRefundHandler(_mockUow.Object, _mockPaymentService.Object, _mockRefundLogger.Object);
        var payment = new Payment { UserId = Guid.NewGuid(), Status = PaymentStatus.Pending };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new ProcessRefundCommand(payment.UserId, new RefundRequestDto(Guid.NewGuid(), 10, "R")));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRefundHandler_ShouldSucceed()
    {
        var handler = new ProcessRefundHandler(_mockUow.Object, _mockPaymentService.Object, _mockRefundLogger.Object);
        var payment = new Payment { UserId = Guid.NewGuid(), Status = PaymentStatus.Completed, Amount = 100 };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        
        _mockPaymentService.Setup(p => p.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RefundResult { Success = true, RefundedAmount = 50 });

        var res = await handler.HandleAsync(new ProcessRefundCommand(payment.UserId, new RefundRequestDto(Guid.NewGuid(), 50, "R")));

        res.Success.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.PartialRefund);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
