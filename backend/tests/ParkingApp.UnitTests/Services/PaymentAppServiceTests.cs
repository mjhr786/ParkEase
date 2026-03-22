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

public class PaymentAppServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IPaymentRepository> _mockPaymentRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<INotificationService> _mockNotification;
    private readonly Mock<ILogger<PaymentAppService>> _mockLogger;
    private readonly Mock<IEmailService> _mockEmail;
    private readonly PaymentAppService _service;

    public PaymentAppServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockPaymentRepo = new Mock<IPaymentRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockUserRepo = new Mock<IUserRepository>();

        _mockUow.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);

        _mockPaymentService = new Mock<IPaymentService>();
        _mockNotification = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<PaymentAppService>>();
        _mockEmail = new Mock<IEmailService>();

        _service = new PaymentAppService(_mockUow.Object, _mockPaymentService.Object, _mockNotification.Object, _mockLogger.Object, _mockEmail.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFail_WhenNotFound()
    {
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        var res = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSuccess_WhenValid()
    {
        var userId = Guid.NewGuid();
        var payment = new Payment { Booking = new Booking { UserId = userId } };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        var res = await _service.GetByIdAsync(Guid.NewGuid(), userId);
        res.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnFail_WhenBookingNotFound()
    {
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking)null);
        var res = await _service.ProcessPaymentAsync(Guid.NewGuid(), new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard));
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSuccess_WhenValid()
    {
        var userId = Guid.NewGuid();
        var booking = new Booking { UserId = userId, Status = BookingStatus.AwaitingPayment, TotalAmount = 100 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        
        var paymentResult = new PaymentResult { Success = true, Status = PaymentStatus.Completed, TransactionId = "TXN" };
        _mockPaymentService.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(paymentResult);

        var res = await _service.ProcessPaymentAsync(userId, new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard));

        res.Success.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var payment = new Payment { UserId = userId, Status = PaymentStatus.Completed, Amount = 100 };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        
        var refundResult = new RefundResult { Success = true, RefundTransactionId = "REF", RefundedAmount = 50 };
        _mockPaymentService.Setup(p => p.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(refundResult);

        var res = await _service.ProcessRefundAsync(userId, new RefundRequestDto(Guid.NewGuid(), 50, "Reason"));

        res.Success.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.PartialRefund);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
