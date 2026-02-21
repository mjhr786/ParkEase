using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Payments;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class PaymentTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPaymentRepository> _mockPaymentRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<ProcessPaymentHandler>> _mockProcessLogger;
    private readonly Mock<ILogger<VerifyPaymentHandler>> _mockVerifyLogger;

    public PaymentTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPaymentRepository = new Mock<IPaymentRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();
        _mockPaymentService = new Mock<IPaymentService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockProcessLogger = new Mock<ILogger<ProcessPaymentHandler>>();
        _mockVerifyLogger = new Mock<ILogger<VerifyPaymentHandler>>();

        _mockUnitOfWork.Setup(u => u.Payments).Returns(_mockPaymentRepository.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepository.Object);
        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepository.Object);
    }

    [Fact]
    public async Task ProcessPaymentHandler_WhenBookingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ProcessPaymentHandler(_mockUnitOfWork.Object, _mockPaymentService.Object, _mockNotificationService.Object, _mockEmailService.Object, _mockProcessLogger.Object);
        _mockBookingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking?)null);

        var dto = new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard);

        // Act
        var result = await handler.HandleAsync(new ProcessPaymentCommand(Guid.NewGuid(), dto));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
    }

    [Fact]
    public async Task ProcessPaymentHandler_WhenDirectPaymentSucceeds_ShouldUpdateStatus()
    {
        // Arrange
        var handler = new ProcessPaymentHandler(_mockUnitOfWork.Object, _mockPaymentService.Object, _mockNotificationService.Object, _mockEmailService.Object, _mockProcessLogger.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = BookingStatus.AwaitingPayment, 
            TotalAmount = 500,
            BookingReference = "REF123"
        };
        
        _mockBookingRepository.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepository.Setup(r => r.GetByBookingIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        
        _mockPaymentService.Setup(s => s.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult 
            { 
                Success = true, 
                TransactionId = "TXN_OK", 
                Status = PaymentStatus.Completed, 
                ReceiptUrl = "http://receipt.url" 
            });

        var dto = new CreatePaymentDto(booking.Id, PaymentMethod.CreditCard);

        // Act
        var result = await handler.HandleAsync(new ProcessPaymentCommand(userId, dto));

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockPaymentRepository.Verify(r => r.AddAsync(It.Is<Payment>(p => p.Status == PaymentStatus.Completed), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyPaymentHandler_WithValidSignature_ShouldConfirmBooking()
    {
        // Arrange
        var handler = new VerifyPaymentHandler(_mockUnitOfWork.Object, _mockPaymentService.Object, _mockNotificationService.Object, _mockEmailService.Object, _mockVerifyLogger.Object);
        var userId = Guid.NewGuid();
        var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.AwaitingPayment };
        
        _mockBookingRepository.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentService.Setup(s => s.VerifyPaymentSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new VerifyPaymentDto 
        { 
            BookingId = booking.Id, 
            RazorpayPaymentId = "pay_123", 
            RazorpayOrderId = "order_123", 
            RazorpaySignature = "sig_123" 
        };

        // Act
        var result = await handler.HandleAsync(new VerifyPaymentCommand(userId, dto));

        // Assert
        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessRefundHandler_WhenValid_ShouldUpdatePaymentStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var payment = new Payment 
        { 
            Id = Guid.NewGuid(), 
            UserId = userId, 
            Status = PaymentStatus.Completed, 
            Amount = 1000 
        };
        
        _mockPaymentRepository.Setup(r => r.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _mockPaymentService.Setup(s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundResult { Success = true, RefundedAmount = 1000, RefundTransactionId = "REF_123" });

        var dto = new RefundRequestDto(payment.Id, 1000, "User cancelled");

        // Act
        var handler = new ProcessRefundHandler(_mockUnitOfWork.Object, _mockPaymentService.Object, new Mock<ILogger<ProcessRefundHandler>>().Object);
        var result = await handler.HandleAsync(new ProcessRefundCommand(userId, dto));

        // Assert
        result.Success.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundTransactionId.Should().Be("REF_123");
    }
}
