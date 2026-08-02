using FluentAssertions;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.IntegrationTests.Payments;

/// <summary>
/// API P3 — application-layer integration: create-order → verify → refund happy path
/// and critical failure cases, using a deterministic payment gateway.
/// </summary>
public class PaymentLifecycleIntegrationTests
{
    [Fact]
    public async Task CreateOrder_Verify_Refund_HappyPath()
    {
        var fx = new InMemoryMarketplaceFixture();
        var userId = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedAwaitingPaymentBooking(userId, parking.Id, amount: 250m);

        // 1) Create payment order (PI)
        var orderResult = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(userId, booking.Id));

        orderResult.Success.Should().BeTrue();
        orderResult.Data.Should().NotBeNullOrWhiteSpace();
        fx.PaymentService.LastOrderAmount.Should().Be(250m);
        fx.PaymentService.LastOrderNotes.Should().ContainKey("bookingId")
            .WhoseValue.Should().Be(booking.Id.ToString());
        fx.PaymentService.LastOrderNotes.Should().ContainKey("purpose")
            .WhoseValue.Should().Be("booking");

        var orderId = orderResult.Data!;
        var paymentIntentId = $"pi_client_{Guid.NewGuid():N}"[..22];

        // 2) Client confirms with Stripe Elements; server verifies signature + confirms booking
        var verifyResult = await fx.VerifyHandler().HandleAsync(new VerifyPaymentCommand(
            userId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = paymentIntentId,
                RazorpayOrderId = orderId,
                RazorpaySignature = "test_sig"
            }));

        verifyResult.Success.Should().BeTrue();
        verifyResult.Data.Should().NotBeNull();
        verifyResult.Data!.Success.Should().BeTrue();
        verifyResult.Data.Status.Should().Be(PaymentStatus.Completed);
        booking.Status.Should().Be(BookingStatus.Confirmed);
        fx.AllPayments.Should().HaveCount(1);
        var payment = fx.AllPayments.Single();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.TransactionId.Should().Be(paymentIntentId);
        payment.Amount.Should().Be(250m);

        // 3) Idempotent re-verify does not create a second payment
        var verifyAgain = await fx.VerifyHandler().HandleAsync(new VerifyPaymentCommand(
            userId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = paymentIntentId,
                RazorpayOrderId = orderId,
                RazorpaySignature = "test_sig"
            }));

        verifyAgain.Success.Should().BeTrue();
        fx.AllPayments.Should().HaveCount(1);

        // 4) Refund completed payment
        var refundResult = await fx.RefundHandler().HandleAsync(new ProcessRefundCommand(
            userId,
            new RefundRequestDto(payment.Id, 250m, "Customer cancelled")));

        refundResult.Success.Should().BeTrue();
        refundResult.Data.Should().NotBeNull();
        refundResult.Data!.Success.Should().BeTrue();
        refundResult.Data.RefundedAmount.Should().Be(250m);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundAmount.Should().Be(250m);
        fx.PaymentService.LastRefundedGatewayTxnId.Should().Be(paymentIntentId);
    }

    [Fact]
    public async Task CreateOrder_RejectsWhenBookingNotAwaitingPayment()
    {
        var fx = new InMemoryMarketplaceFixture();
        var userId = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedCompletedMarketplaceBooking(userId, parking.Id);

        var result = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(userId, booking.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking is not awaiting payment");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrder_RejectsForeignUser()
    {
        var fx = new InMemoryMarketplaceFixture();
        var ownerUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedAwaitingPaymentBooking(ownerUser, parking.Id);

        var result = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(otherUser, booking.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task Verify_RejectsInvalidSignature()
    {
        var fx = new InMemoryMarketplaceFixture();
        fx.PaymentService.VerifySignatureResult = false;
        var userId = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedAwaitingPaymentBooking(userId, parking.Id);

        var order = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(userId, booking.Id));
        order.Success.Should().BeTrue();

        var result = await fx.VerifyHandler().HandleAsync(new VerifyPaymentCommand(
            userId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_bad",
                RazorpayOrderId = order.Data,
                RazorpaySignature = "bad"
            }));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid payment signature");
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
        fx.AllPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task Refund_RejectsNonOwner()
    {
        var fx = new InMemoryMarketplaceFixture();
        var userId = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedAwaitingPaymentBooking(userId, parking.Id, amount: 100m);

        var order = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(userId, booking.Id));
        var pi = "pi_owned_1";
        await fx.VerifyHandler().HandleAsync(new VerifyPaymentCommand(
            userId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = pi,
                RazorpayOrderId = order.Data,
                RazorpaySignature = "ok"
            }));

        var payment = fx.AllPayments.Single();
        var result = await fx.RefundHandler().HandleAsync(new ProcessRefundCommand(
            attacker,
            new RefundRequestDto(payment.Id, 100m, "Fraud attempt")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task Refund_RejectsWhenGatewayFails()
    {
        var fx = new InMemoryMarketplaceFixture();
        fx.PaymentService.RefundSucceeds = false;
        var userId = Guid.NewGuid();
        var parking = fx.SeedPublicParking();
        var booking = fx.SeedAwaitingPaymentBooking(userId, parking.Id, amount: 80m);

        var order = await fx.CreateOrderHandler()
            .HandleAsync(new CreatePaymentOrderCommand(userId, booking.Id));
        await fx.VerifyHandler().HandleAsync(new VerifyPaymentCommand(
            userId,
            new VerifyPaymentDto
            {
                BookingId = booking.Id,
                RazorpayPaymentId = "pi_refund_fail",
                RazorpayOrderId = order.Data,
                RazorpaySignature = "ok"
            }));

        var payment = fx.AllPayments.Single();
        var result = await fx.RefundHandler().HandleAsync(new ProcessRefundCommand(
            userId,
            new RefundRequestDto(payment.Id, 80m, "Changed plans")));

        result.Success.Should().BeFalse();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.RefundAmount.Should().BeNull();
    }
}
