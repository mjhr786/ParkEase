using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class PaymentCaptureOrchestratorTests
{
    [Fact]
    public async Task Capture_WhenAlreadyCapturedWithDifferentId_DoesNotConfirm()
    {
        var booking = CreateAwaitingPaymentBooking();
        booking.Confirm();
        var payment = Payment.CreatePending(booking.Id, booking.UserId, 50m, PaymentMethod.CreditCard);
        payment.MarkSucceeded("pi_first", "ord_first", "Stripe", amount: 50m);

        var (orchestrator, uow, _) = CreateSut(booking, payment);

        var result = await orchestrator.CaptureAsync(new PaymentCaptureCommand(
            booking.Id,
            "booking",
            "pi_second",
            "ord_second",
            50m,
            "INR",
            "Stripe",
            PaymentMethod.CreditCard));

        result.Outcome.Should().Be(PaymentCaptureOutcome.AlreadyCapturedDifferentId);
        result.NeedsRefund.Should().BeTrue();
        result.Success.Should().BeFalse();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        payment.TransactionId.Should().Be("pi_first");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_WhenBookingCannotAccept_ReturnsCannotAccept()
    {
        var booking = CreateAwaitingPaymentBooking();
        booking.Cancel("changed plans");

        var (orchestrator, uow, _) = CreateSut(booking, payment: null);

        var result = await orchestrator.CaptureAsync(new PaymentCaptureCommand(
            booking.Id,
            "booking",
            "pi_late",
            "ord_late",
            50m,
            "INR",
            "Stripe",
            PaymentMethod.CreditCard));

        result.Outcome.Should().Be(PaymentCaptureOutcome.CannotAccept);
        result.NeedsRefund.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_WhenPurposeDoesNotMatchBookingState_FailsClosed()
    {
        var booking = CreateAwaitingPaymentBooking();
        var (orchestrator, uow, _) = CreateSut(booking, payment: null);

        var result = await orchestrator.CaptureAsync(new PaymentCaptureCommand(
            booking.Id,
            "extension",
            "pi_ext",
            "ord_ext",
            20m,
            "INR",
            "Stripe",
            PaymentMethod.CreditCard));

        result.Outcome.Should().Be(PaymentCaptureOutcome.PurposeMismatch);
        result.Success.Should().BeFalse();
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_WhenExtensionMissingPendingFields_ReturnsCannotAccept()
    {
        var booking = CreateConfirmedBooking();
        booking.Status = BookingStatus.AwaitingExtensionPayment;

        var (orchestrator, uow, store) = CreateSut(booking, payment: null);

        var result = await orchestrator.CaptureAsync(ExtensionCommand(booking, "pi_ext"));

        result.Outcome.Should().Be(PaymentCaptureOutcome.CannotAccept);
        result.NeedsRefund.Should().BeTrue();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.ReloadAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Capture_WhenConfirmExtensionThrows_ReloadsBookingAndReclassifiesToAlreadyCaptured()
    {
        var booking = CreateConfirmedBooking();
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 40m);
        booking.ApproveExtension();
        var originalEnd = booking.EndDateTime;

        var (orchestrator, uow, store) = CreateSut(booking, payment: null);
        store.Setup(s => s.FindLiveOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                booking.ConfirmExtension();
                booking.RecordExtensionFeePaid("pi_ext");
            })
            .ReturnsAsync((PaymentOrder?)null);

        var result = await orchestrator.CaptureAsync(ExtensionCommand(booking, "pi_ext"));

        result.Outcome.Should().Be(PaymentCaptureOutcome.AlreadyCaptured);
        result.Success.Should().BeTrue();
        result.NeedsRefund.Should().BeFalse();
        result.TransactionId.Should().Be("pi_ext");
        booking.ExtensionFeeTransactionId.Should().Be("pi_ext");
        booking.EndDateTime.Should().Be(originalEnd.AddHours(1));
        store.Verify(s => s.ReloadAsync(booking, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (PaymentCaptureOrchestrator Orchestrator, Mock<IMarketplaceUnitOfWork> Uow, Mock<IPaymentCheckoutStore> Store)
        CreateSut(Booking booking, Payment? payment)
    {
        var uow = new Mock<IMarketplaceUnitOfWork>();
        var bookings = new Mock<IBookingRepository>();
        var payments = new Mock<IPaymentRepository>();
        var parking = new Mock<IParkingSpaceRepository>();
        uow.Setup(u => u.Bookings).Returns(bookings.Object);
        uow.Setup(u => u.Payments).Returns(payments.Object);
        uow.Setup(u => u.ParkingSpaces).Returns(parking.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        bookings.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        payments.Setup(r => r.GetByBookingIdAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        var store = new Mock<IPaymentCheckoutStore>();
        store.Setup(s => s.FindLiveOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.FindCapturedOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.FindByProviderPaymentIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrder?)null);
        store.Setup(s => s.ReloadAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var orchestrator = new PaymentCaptureOrchestrator(
            uow.Object,
            store.Object,
            NullLogger<PaymentCaptureOrchestrator>.Instance);
        return (orchestrator, uow, store);
    }

    private static Booking CreateConfirmedBooking()
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            50m,
            0,
            0,
            0,
            50m);
        booking.Confirm();
        return booking;
    }

    private static PaymentCaptureCommand ExtensionCommand(Booking booking, string paymentId) =>
        new(booking.Id, "extension", paymentId, "ord_ext", 40m, "INR", "Stripe", PaymentMethod.CreditCard);

    private static Booking CreateAwaitingPaymentBooking()
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            50m,
            0,
            0,
            0,
            50m);
        booking.AwaitPayment();
        return booking;
    }
}
