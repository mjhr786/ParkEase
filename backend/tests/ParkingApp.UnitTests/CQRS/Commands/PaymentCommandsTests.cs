using ParkingApp.Application.Contracts.Notifications;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using IPaymentService = ParkingApp.Marketplace.Application.Interfaces.IPaymentService;
using IEmailService = ParkingApp.Application.Interfaces.IEmailService;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
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
    private readonly Mock<IStripeCheckoutService> _mockStripeCheckout;
    private readonly Mock<IPaymentCheckoutStore> _mockCheckoutStore;
    private readonly Mock<ParkingApp.Application.Interfaces.ICacheService> _mockCache;
    private readonly Mock<INotificationSender> _mockNotification;
    private readonly Mock<IEmailService> _mockEmail;
    
    private readonly Mock<ILogger<ProcessPaymentHandler>> _mockProcessLogger;
    private readonly Mock<ILogger<CreatePaymentOrderHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<VerifyPaymentHandler>> _mockVerifyLogger;
    private readonly Mock<ILogger<ProcessRefundHandler>> _mockRefundLogger;
    private readonly Mock<IUpiCheckoutService> _mockUpiCheckout;

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
        _mockStripeCheckout = new Mock<IStripeCheckoutService>();
        _mockCheckoutStore = new Mock<IPaymentCheckoutStore>();
        _mockCache = new Mock<ParkingApp.Application.Interfaces.ICacheService>();
        _mockCache
            .Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockCache
            .Setup(c => c.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockCheckoutStore
            .Setup(s => s.ListLiveOrdersAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PaymentOrder>());
        _mockNotification = new Mock<INotificationSender>();
        _mockEmail = new Mock<IEmailService>();

        _mockProcessLogger = new Mock<ILogger<ProcessPaymentHandler>>();
        _mockCreateLogger = new Mock<ILogger<CreatePaymentOrderHandler>>();
        _mockVerifyLogger = new Mock<ILogger<VerifyPaymentHandler>>();
        _mockRefundLogger = new Mock<ILogger<ProcessRefundHandler>>();
        _mockUpiCheckout = new Mock<IUpiCheckoutService>();
    }

    // ProcessPaymentHandler Tests
    [Fact]
    public async Task ProcessPaymentHandler_ShouldFail_WhenBookingNotFound()
    {
        var handler = CreateProcessHandler();
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Booking)null);
        
        var res = await handler.HandleAsync(new ProcessPaymentCommand(Guid.NewGuid(), new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard)));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentHandler_ShouldSucceed()
    {
        var handler = CreateProcessHandler();
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50, ParkingSpaceId = Guid.NewGuid() };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        
        var paymentResult = new PaymentResult { Success = true, Status = PaymentStatus.Completed, TransactionId = "TXN" };
        _mockPaymentService.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(paymentResult);

        var res = await handler.HandleAsync(new ProcessPaymentCommand(booking.UserId, new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard)));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentHandler_Extension_DoesNotOverwriteBookingTransactionId()
    {
        var handler = CreateProcessHandler();
        var booking = CreateConfirmedBooking(totalAmount: 100m);
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 40m);
        booking.ApproveExtension();
        var existing = Payment.CreatePending(booking.Id, booking.UserId, 100m, PaymentMethod.CreditCard);
        existing.MarkSucceeded("pi_original", "order_original", "Stripe", amount: 100m);

        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _mockPaymentService.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult { Success = true, Status = PaymentStatus.Completed, TransactionId = "pi_ext_mock" });

        var res = await handler.HandleAsync(new ProcessPaymentCommand(
            booking.UserId,
            new CreatePaymentDto(booking.Id, PaymentMethod.CreditCard)));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ExtensionFeeTransactionId.Should().Be("pi_ext_mock");
        existing.TransactionId.Should().Be("pi_original");
    }

    [Fact]
    public async Task ProcessPaymentHandler_Extension_GatewayFailure_DoesNotMutateCompletedBookingPayment()
    {
        var handler = CreateProcessHandler();
        var booking = CreateConfirmedBooking(totalAmount: 100m);
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 40m);
        booking.ApproveExtension();
        var existing = Payment.CreatePending(booking.Id, booking.UserId, 100m, PaymentMethod.CreditCard);
        existing.MarkSucceeded("pi_original", "order_original", "Stripe", amount: 100m);

        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _mockPaymentService.Setup(p => p.ProcessPaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                TransactionId = "pi_failed",
                ErrorMessage = "declined"
            });

        var res = await handler.HandleAsync(new ProcessPaymentCommand(
            booking.UserId,
            new CreatePaymentDto(booking.Id, PaymentMethod.CreditCard)));

        res.Success.Should().BeFalse();
        res.Data.Should().NotBeNull();
        res.Data!.Success.Should().BeFalse();
        res.Data.Status.Should().Be(PaymentStatus.Failed);
        res.Data.Message.Should().Be("declined");
        existing.Status.Should().Be(PaymentStatus.Completed);
        existing.TransactionId.Should().Be("pi_original");
        existing.PaymentGateway.Should().Be("Stripe");
        booking.Status.Should().Be(BookingStatus.AwaitingExtensionPayment);
        booking.ExtensionFeeTransactionId.Should().BeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPaymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
    }

    // CreatePaymentOrderHandler Tests
    [Fact]
    public async Task CreatePaymentOrderHandler_ShouldFail_WhenBookingWrongStatus()
    {
        var handler = new CreatePaymentOrderHandler(
            _mockUow.Object, _mockStripeCheckout.Object, _mockCheckoutStore.Object, _mockCache.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.Pending };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not awaiting payment");
    }

    [Fact]
    public async Task CreatePaymentOrderHandler_ShouldSucceed()
    {
        var handler = new CreatePaymentOrderHandler(
            _mockUow.Object, _mockStripeCheckout.Object, _mockCheckoutStore.Object, _mockCache.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockStripeCheckout
            .Setup(p => p.CreateCardSessionAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardCheckoutSession("pi_abc", "pi_abc_secret_xyz"));

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, Guid.NewGuid()));

        res.Success.Should().BeTrue();
        res.Data.Should().Be("pi_abc_secret_xyz");
        _mockCheckoutStore.Verify(s => s.Add(It.Is<PaymentOrder>(o =>
            o.Rail == PaymentOrder.RailStripe
            && o.Provider == PaymentOrder.ProviderStripe
            && o.ProviderOrderId == "pi_abc"
            && o.Status == PaymentOrderStatus.Created)), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentOrderHandler_CancelsLiveUpiOrder_BeforeCreatingStripe()
    {
        var liveUpi = PaymentOrder.Create(
            Guid.NewGuid(), "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_live", 50, "INR", PaymentOrderStatus.Pending, "pay_live", DateTime.UtcNow.AddMinutes(10));
        _mockCheckoutStore
            .Setup(s => s.ListLiveOrdersAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { liveUpi });
        _mockStripeCheckout
            .Setup(p => p.CreateCardSessionAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CardCheckoutSession("pi_new", "pi_new_secret"));
        var handler = new CreatePaymentOrderHandler(
            _mockUow.Object, _mockStripeCheckout.Object, _mockCheckoutStore.Object, _mockCache.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, booking.Id));

        res.Success.Should().BeTrue();
        liveUpi.Status.Should().Be(PaymentOrderStatus.Cancelled);
        _mockCheckoutStore.Verify(s => s.Update(liveUpi), Times.Once);
    }

    [Fact]
    public async Task CreatePaymentOrderHandler_WhenLockNotAcquired_ReturnsPaymentInProgress()
    {
        _mockCache
            .Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CreatePaymentOrderHandler(
            _mockUow.Object, _mockStripeCheckout.Object, _mockCheckoutStore.Object, _mockCache.Object, _mockCreateLogger.Object);
        var booking = new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.AwaitingPayment, TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new CreatePaymentOrderCommand(booking.UserId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("payment_in_progress");
        _mockStripeCheckout.Verify(
            p => p.CreateCardSessionAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // VerifyPaymentHandler Tests
    [Fact]
    public async Task VerifyPaymentHandler_ShouldFail_WhenInvalidSignature()
    {
        var handler = CreateVerifyHandler();
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50 };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.Is<PaymentCaptureCheck>(c =>
                c.BookingId == booking.Id && c.Amount == 50m && c.Currency == "INR" && c.Purpose == "booking")))
            .ReturnsAsync(false);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(booking.UserId, new VerifyPaymentDto { BookingId = Guid.NewGuid(), RazorpayPaymentId = "p", RazorpayOrderId = "o", RazorpaySignature = "s" }));

        res.Success.Should().BeFalse();
        _mockPaymentService.Verify(p => p.VerifyPaymentSignatureAsync(
            "p",
            "o",
            "s",
            It.IsAny<CancellationToken>(),
            It.Is<PaymentCaptureCheck>(c =>
                c.BookingId == booking.Id && c.Amount == 50m && c.Currency == "INR" && c.Purpose == "booking")),
            Times.Once);
    }

    [Fact]
    public async Task VerifyPaymentHandler_ShouldSucceed()
    {
        var handler = CreateVerifyHandler();
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50, ParkingSpaceId = Guid.NewGuid() };
        Payment? captured = null;
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment p, CancellationToken _) =>
            {
                captured = p;
                return p;
            });
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.Is<PaymentCaptureCheck>(c =>
                c.BookingId == booking.Id && c.Amount == 50m && c.Currency == "INR" && c.Purpose == "booking")))
            .ReturnsAsync(true);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(booking.UserId, new VerifyPaymentDto { BookingId = Guid.NewGuid(), RazorpayPaymentId = "p", RazorpayOrderId = "o", RazorpaySignature = "s" }));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        captured.Should().NotBeNull();
        captured!.PaymentGateway.Should().Be("Stripe");
        captured.TransactionId.Should().Be("p");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockPaymentService.Verify(p => p.VerifyPaymentSignatureAsync(
            "p",
            "o",
            "s",
            It.IsAny<CancellationToken>(),
            It.Is<PaymentCaptureCheck>(c =>
                c.BookingId == booking.Id && c.Amount == 50m && c.Currency == "INR" && c.Purpose == "booking")),
            Times.Once);
    }

    [Fact]
    public async Task VerifyPaymentHandler_Extension_WithCompletedBookingPayment_DoesNotConfirmExtensionWithoutNewCharge()
    {
        var handler = CreateVerifyHandler();
        var booking = CreateConfirmedBooking(totalAmount: 100m);
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 40m);
        booking.ApproveExtension();
        var originalEnd = booking.EndDateTime;

        var existing = Payment.CreatePending(booking.Id, booking.UserId, 100m, PaymentMethod.CreditCard);
        existing.MarkSucceeded("pi_original", "order_original", "Stripe", amount: 100m);
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<PaymentCaptureCheck>()))
            .ReturnsAsync(true);

        var missingFields = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto { BookingId = booking.Id }));

        missingFields.Success.Should().BeFalse();
        missingFields.Message.Should().Contain("required");
        booking.Status.Should().Be(BookingStatus.AwaitingExtensionPayment);
        booking.EndDateTime.Should().Be(originalEnd);
        booking.ExtensionFeeTransactionId.Should().BeNull();
        existing.TransactionId.Should().Be("pi_original");

        var replayOriginal = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_original",
                RazorpayOrderId = "order_original",
                RazorpaySignature = "s"
            }));

        replayOriginal.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.AwaitingExtensionPayment);
        booking.EndDateTime.Should().Be(originalEnd);
        booking.ExtensionFeeTransactionId.Should().BeNull();
        existing.TransactionId.Should().Be("pi_original");
        _mockPaymentService.Verify(
            p => p.VerifyPaymentSignatureAsync(
                "pi_original",
                "order_original",
                "s",
                It.IsAny<CancellationToken>(),
                It.Is<PaymentCaptureCheck>(c => c.Purpose == "extension" && c.Amount == 40m)),
            Times.Once);
    }

    [Fact]
    public async Task VerifyPaymentHandler_ExtensionCapture_PersistsExtensionFeeTransactionId_DoesNotOverwriteBookingTransactionId()
    {
        var handler = CreateVerifyHandler();
        var booking = CreateConfirmedBooking(totalAmount: 100m);
        booking.RequestExtension(booking.EndDateTime.AddHours(2), 40m);
        booking.ApproveExtension();

        var existing = Payment.CreatePending(booking.Id, booking.UserId, 100m, PaymentMethod.CreditCard);
        existing.MarkSucceeded("pi_original", "order_original", "Stripe", amount: 100m);
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockPaymentRepo.Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _mockPaymentService.Setup(p => p.VerifyPaymentSignatureAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.Is<PaymentCaptureCheck>(c => c.Purpose == "extension" && c.Amount == 40m)))
            .ReturnsAsync(true);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_extension",
                RazorpayOrderId = "order_extension",
                RazorpaySignature = "s"
            }));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ExtensionFeeTransactionId.Should().Be("pi_extension");
        existing.TransactionId.Should().Be("pi_original");
        existing.PaymentGateway.Should().Be("Stripe");
        existing.Status.Should().Be(PaymentStatus.Completed);
        booking.DomainEvents.Should().Contain(e => e is ParkingApp.Marketplace.Domain.Events.PaymentCompletedEvent);
    }

    [Fact]
    public async Task VerifyPaymentHandler_RailMismatch_ReturnsRailMismatch()
    {
        var handler = CreateVerifyHandler();
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50, Status = BookingStatus.AwaitingPayment };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_abc",
                RazorpayOrderId = "pi_abc",
                RazorpaySignature = "s",
                Rail = "upi"
            }));

        res.Success.Should().BeFalse();
        res.Code.Should().Be(PaymentErrorCodes.RailMismatch);
    }

    [Fact]
    public async Task VerifyPaymentHandler_LockNotAcquired_ReturnsPaymentInProgress()
    {
        _mockCache
            .Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateVerifyHandler();
        var booking = new Booking { UserId = Guid.NewGuid(), TotalAmount = 50, Status = BookingStatus.AwaitingPayment };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_abc",
                RazorpayOrderId = "pi_abc",
                RazorpaySignature = "s"
            }));

        res.Success.Should().BeFalse();
        res.Code.Should().Be(PaymentErrorCodes.PaymentInProgress);
    }

    [Fact]
    public async Task VerifyPaymentHandler_UpiCaptured_ConfirmsWithoutSignature()
    {
        var handler = CreateVerifyHandler();
        var booking = new Booking
        {
            UserId = Guid.NewGuid(),
            TotalAmount = 50,
            ParkingSpaceId = Guid.NewGuid(),
            Status = BookingStatus.AwaitingPayment
        };
        _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _mockUpiCheckout
            .Setup(u => u.GetPaymentAsync("pay_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpiPaymentSnapshot(
                "pay_abc",
                "captured",
                5000,
                "INR",
                "order_abc",
                new Dictionary<string, string>
                {
                    ["bookingId"] = booking.Id.ToString(),
                    ["purpose"] = "booking"
                }));
        _mockUpiCheckout
            .Setup(u => u.MatchesExpected(It.IsAny<UpiPaymentSnapshot>(), It.IsAny<PaymentCaptureCheck>()))
            .Returns(true);

        var res = await handler.HandleAsync(new VerifyPaymentCommand(
            booking.UserId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pay_abc",
                RazorpayOrderId = "order_abc"
            }));

        res.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _mockPaymentService.Verify(
            p => p.VerifyPaymentSignatureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<PaymentCaptureCheck>()),
            Times.Never);
    }

    private ProcessPaymentHandler CreateProcessHandler()
    {
        var orchestrator = new PaymentCaptureOrchestrator(
            _mockUow.Object,
            CreateCheckoutStore().Object,
            NullLogger<PaymentCaptureOrchestrator>.Instance);

        return new ProcessPaymentHandler(
            _mockUow.Object,
            _mockPaymentService.Object,
            new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object,
            orchestrator,
            _mockProcessLogger.Object);
    }

    private VerifyPaymentHandler CreateVerifyHandler()
    {
        var store = CreateCheckoutStore();
        var orchestrator = new PaymentCaptureOrchestrator(
            _mockUow.Object,
            store.Object,
            NullLogger<PaymentCaptureOrchestrator>.Instance);

        return new VerifyPaymentHandler(
            _mockUow.Object,
            _mockPaymentService.Object,
            _mockCache.Object,
            store.Object,
            _mockUpiCheckout.Object,
            orchestrator,
            _mockVerifyLogger.Object);
    }

    private static Mock<IPaymentCheckoutStore> CreateCheckoutStore()
    {
        var store = new Mock<IPaymentCheckoutStore>();
        store.Setup(s => s.FindLiveOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.FindCapturedOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.FindByProviderPaymentIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.FindByProviderOrderIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.LockOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.ReloadAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return store;
    }

    private static Booking CreateConfirmedBooking(decimal totalAmount)
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            totalAmount,
            0,
            0,
            0,
            totalAmount);
        booking.Confirm();
        return booking;
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
        // TransactionId is required — handler rejects refunds without a gateway transaction id
        var payment = new Payment
        {
            UserId = Guid.NewGuid(),
            Status = PaymentStatus.Completed,
            Amount = 100,
            TransactionId = "txn_test_123"
        };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        
        _mockPaymentService.Setup(p => p.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RefundResult { Success = true, RefundedAmount = 50 });

        var res = await handler.HandleAsync(new ProcessRefundCommand(payment.UserId, new RefundRequestDto(Guid.NewGuid(), 50, "R")));

        res.Success.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.PartialRefund);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundHandler_ShouldFail_WhenPaymentHasNoTransactionId()
    {
        var handler = new ProcessRefundHandler(_mockUow.Object, _mockPaymentService.Object, _mockRefundLogger.Object);
        var payment = new Payment { UserId = Guid.NewGuid(), Status = PaymentStatus.Completed, Amount = 100 };
        _mockPaymentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var res = await handler.HandleAsync(new ProcessRefundCommand(payment.UserId, new RefundRequestDto(Guid.NewGuid(), 50, "R")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("gateway transaction id");
        _mockPaymentService.Verify(
            p => p.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}





