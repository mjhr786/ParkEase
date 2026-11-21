using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Applies a matched provider capture to a booking. Idempotency key is
/// (BookingId, Purpose, ProviderPaymentId) — never "any completed Payment".
/// </summary>
internal sealed class PaymentCaptureOrchestrator : IPaymentCaptureOrchestrator
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly ILogger<PaymentCaptureOrchestrator> _logger;

    public PaymentCaptureOrchestrator(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        ILogger<PaymentCaptureOrchestrator> logger)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _logger = logger;
    }

    public async Task<PaymentCaptureResult> CaptureAsync(
        PaymentCaptureCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.BookingId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.Purpose)
            || string.IsNullOrWhiteSpace(command.ProviderPaymentId))
        {
            return PaymentCaptureResult.Failed("Capture command is incomplete");
        }

        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return PaymentCaptureResult.BookingNotFound();

        var payment = await _unitOfWork.Payments.GetByBookingIdAsync(command.BookingId, cancellationToken);
        var capturedOrder = await LoadCapturedOrderAsync(command, cancellationToken);

        var classified = Classify(command, booking, payment, capturedOrder);
        if (classified is not null)
            return classified;

        try
        {
            return await ApplyAsync(command, booking, payment, cancellationToken);
        }
        catch (BusinessRuleException ex)
        {
            await _checkoutStore.ReloadAsync(booking, cancellationToken);
            payment = await _unitOfWork.Payments.GetByBookingIdAsync(command.BookingId, cancellationToken);
            capturedOrder = await LoadCapturedOrderAsync(command, cancellationToken);

            var reclassified = Classify(command, booking, payment, capturedOrder);
            if (reclassified is not null)
                return reclassified;

            _logger.LogWarning(
                ex,
                "Capture Confirm threw after apply classification for booking {BookingId} purpose {Purpose} payment {ProviderPaymentId}",
                command.BookingId,
                command.Purpose,
                command.ProviderPaymentId);
            return PaymentCaptureResult.Failed(ex.Message);
        }
    }

    private async Task<PaymentOrder?> LoadCapturedOrderAsync(
        PaymentCaptureCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.PaymentGateway)
            && !string.IsNullOrWhiteSpace(command.ProviderPaymentId))
        {
            var byPaymentId = await _checkoutStore.FindByProviderPaymentIdAsync(
                command.PaymentGateway,
                command.ProviderPaymentId,
                cancellationToken);
            if (byPaymentId is { Status: PaymentOrderStatus.Captured }
                && byPaymentId.BookingId == command.BookingId
                && string.Equals(byPaymentId.Purpose, command.Purpose, StringComparison.Ordinal))
            {
                return byPaymentId;
            }
        }

        return await _checkoutStore.FindCapturedOrderAsync(command.BookingId, command.Purpose, cancellationToken);
    }

    private static PaymentCaptureResult? Classify(
        PaymentCaptureCommand command,
        Booking booking,
        Payment? payment,
        PaymentOrder? capturedOrder)
    {
        var providerPaymentId = command.ProviderPaymentId;
        var purpose = command.Purpose;

        // (a) already captured/confirmed with THIS ProviderPaymentId
        if (string.Equals(booking.OverstayFeeTransactionId, providerPaymentId, StringComparison.Ordinal))
        {
            return PaymentCaptureResult.AlreadyCaptured(
                providerPaymentId,
                AlreadyCapturedMessage(PaymentChargeResolver.PurposeOverstay),
                payment?.ReceiptUrl);
        }

        if (string.Equals(booking.ExtensionFeeTransactionId, providerPaymentId, StringComparison.Ordinal))
        {
            return PaymentCaptureResult.AlreadyCaptured(
                providerPaymentId,
                AlreadyCapturedMessage(PaymentChargeResolver.PurposeExtension),
                payment?.ReceiptUrl);
        }

        if (payment is { Status: PaymentStatus.Completed }
            && string.Equals(payment.TransactionId, providerPaymentId, StringComparison.Ordinal)
            && !CanAccept(booking, PaymentChargeResolver.PurposeBooking))
        {
            return PaymentCaptureResult.AlreadyCaptured(
                providerPaymentId,
                AlreadyCapturedMessage(PaymentChargeResolver.PurposeBooking),
                payment.ReceiptUrl);
        }

        if (capturedOrder is { Status: PaymentOrderStatus.Captured }
            && string.Equals(capturedOrder.ProviderPaymentId, providerPaymentId, StringComparison.Ordinal))
        {
            return PaymentCaptureResult.AlreadyCaptured(
                providerPaymentId,
                AlreadyCapturedMessage(capturedOrder.Purpose),
                payment?.ReceiptUrl);
        }

        var expected = PaymentChargeResolver.ResolveExpectedCapture(booking);
        if (!string.Equals(purpose, expected.Purpose, StringComparison.Ordinal))
            return PaymentCaptureResult.PurposeMismatch("Payment purpose does not match booking state");

        var recordedForPurpose = RecordedCaptureIdForPurpose(booking, payment, capturedOrder, purpose);

        // (c) already captured with a DIFFERENT id
        if (!string.IsNullOrWhiteSpace(recordedForPurpose)
            && !string.Equals(recordedForPurpose, providerPaymentId, StringComparison.Ordinal)
            && !AllowsAdditionalCapture(booking, purpose))
        {
            return PaymentCaptureResult.AlreadyCapturedDifferentId(
                "Payment already captured with a different transaction",
                recordedForPurpose);
        }

        // (b) booking cannot accept this purpose
        if (!CanAccept(booking, purpose))
        {
            return PaymentCaptureResult.CannotAccept(
                "Booking cannot accept this payment",
                providerPaymentId);
        }

        return null;
    }

    private async Task<PaymentCaptureResult> ApplyAsync(
        PaymentCaptureCommand command,
        Booking booking,
        Payment? payment,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var purpose = command.Purpose;
        var providerPaymentId = command.ProviderPaymentId;
        var order = await LoadOrderForCaptureAsync(command, purpose, cancellationToken);

        if (string.Equals(purpose, PaymentChargeResolver.PurposeBooking, StringComparison.Ordinal))
        {
            booking.Confirm();
            if (payment is null)
            {
                payment = Payment.CreatePending(
                    booking.Id,
                    booking.UserId,
                    command.Amount,
                    command.PaymentMethod,
                    command.Currency);
                payment.MarkSucceeded(
                    providerPaymentId,
                    command.ProviderOrderId,
                    command.PaymentGateway,
                    amount: command.Amount);
                await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
            }
            else if (payment.Status != PaymentStatus.Completed)
            {
                payment.MarkSucceeded(
                    providerPaymentId,
                    command.ProviderOrderId,
                    command.PaymentGateway,
                    amount: command.Amount);
                _unitOfWork.Payments.Update(payment);
            }

            booking.RecordPaymentCompleted(payment.Id, command.Amount, command.Currency, isExtensionPayment: false);
            await BayAssignmentHelper.TryApplyAsync(_unitOfWork, booking, cancellationToken);
        }
        else if (string.Equals(purpose, PaymentChargeResolver.PurposeExtension, StringComparison.Ordinal))
        {
            booking.ConfirmExtension();
            booking.RecordExtensionFeePaid(providerPaymentId);
            booking.RecordPaymentCompleted(
                payment?.Id ?? Guid.Empty,
                command.Amount,
                command.Currency,
                isExtensionPayment: true);
        }
        else if (string.Equals(purpose, PaymentChargeResolver.PurposeOverstay, StringComparison.Ordinal))
        {
            booking.MarkOverstayFeePaid(command.Amount, providerPaymentId, now);
        }
        else
        {
            return PaymentCaptureResult.PurposeMismatch("Payment purpose does not match booking state");
        }

        TryMarkOrderCaptured(order, command, now);
        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Captured {Purpose} payment {ProviderPaymentId} for booking {BookingId} via {PaymentGateway}",
            purpose,
            providerPaymentId,
            booking.Id,
            command.PaymentGateway);

        return PaymentCaptureResult.Applied(
            providerPaymentId,
            AppliedMessage(purpose),
            payment?.ReceiptUrl);
    }

    private async Task<PaymentOrder?> LoadOrderForCaptureAsync(
        PaymentCaptureCommand command,
        string purpose,
        CancellationToken cancellationToken)
    {
        var liveOrder = await _checkoutStore.FindLiveOrderAsync(command.BookingId, purpose, cancellationToken);
        if (liveOrder is not null)
            return liveOrder;

        if (!string.IsNullOrWhiteSpace(command.PaymentGateway)
            && !string.IsNullOrWhiteSpace(command.ProviderPaymentId))
        {
            var byPaymentId = await _checkoutStore.FindByProviderPaymentIdAsync(
                command.PaymentGateway, command.ProviderPaymentId, cancellationToken);
            if (IsOrderForCapture(byPaymentId, command, purpose))
                return byPaymentId;
        }

        if (!string.IsNullOrWhiteSpace(command.PaymentGateway)
            && !string.IsNullOrWhiteSpace(command.ProviderOrderId))
        {
            var byOrderId = await _checkoutStore.FindByProviderOrderIdAsync(
                command.PaymentGateway, command.ProviderOrderId, cancellationToken);
            if (IsOrderForCapture(byOrderId, command, purpose))
                return byOrderId;
        }

        return null;
    }

    private static bool IsOrderForCapture(PaymentOrder? order, PaymentCaptureCommand command, string purpose) =>
        order is not null
        && order.BookingId == command.BookingId
        && string.Equals(order.Purpose, purpose, StringComparison.Ordinal)
        && order.Status != PaymentOrderStatus.Captured;

    private void TryMarkOrderCaptured(PaymentOrder? order, PaymentCaptureCommand command, DateTime now)
    {
        if (order is null)
            return;
        if (order.BookingId != command.BookingId)
            return;
        if (!string.Equals(order.Purpose, command.Purpose, StringComparison.Ordinal))
            return;
        if (order.Status == PaymentOrderStatus.Captured)
            return;

        order.MarkCaptured(command.ProviderPaymentId, now);
        _checkoutStore.Update(order);
    }

    private static string? RecordedCaptureIdForPurpose(
        Booking booking,
        Payment? payment,
        PaymentOrder? capturedOrder,
        string purpose)
    {
        if (string.Equals(purpose, PaymentChargeResolver.PurposeBooking, StringComparison.Ordinal))
        {
            if (payment is { Status: PaymentStatus.Completed } && !string.IsNullOrWhiteSpace(payment.TransactionId))
                return payment.TransactionId;
        }
        else if (string.Equals(purpose, PaymentChargeResolver.PurposeExtension, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(booking.ExtensionFeeTransactionId))
                return booking.ExtensionFeeTransactionId;
        }
        else if (string.Equals(purpose, PaymentChargeResolver.PurposeOverstay, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(booking.OverstayFeeTransactionId) && booking.OverstayFeeOutstanding <= 0)
                return booking.OverstayFeeTransactionId;
        }

        if (capturedOrder is { Status: PaymentOrderStatus.Captured }
            && !string.IsNullOrWhiteSpace(capturedOrder.ProviderPaymentId)
            && string.Equals(capturedOrder.Purpose, purpose, StringComparison.Ordinal))
        {
            return capturedOrder.ProviderPaymentId;
        }

        return null;
    }

    private static bool AllowsAdditionalCapture(Booking booking, string purpose) =>
        string.Equals(purpose, PaymentChargeResolver.PurposeOverstay, StringComparison.Ordinal)
        && booking.OverstayFeeOutstanding > 0;

    private static bool CanAccept(Booking booking, string purpose)
    {
        if (string.Equals(purpose, PaymentChargeResolver.PurposeBooking, StringComparison.Ordinal))
            return booking.Status is BookingStatus.Pending or BookingStatus.AwaitingPayment;

        if (string.Equals(purpose, PaymentChargeResolver.PurposeExtension, StringComparison.Ordinal))
        {
            if (!booking.PendingExtensionEndDateTime.HasValue || !booking.PendingExtensionAmount.HasValue)
                return false;
            if (booking.Status == BookingStatus.AwaitingExtensionPayment)
                return true;
            return booking.Status == BookingStatus.PendingExtension && booking.PendingExtensionAmount.Value == 0m;
        }

        if (string.Equals(purpose, PaymentChargeResolver.PurposeOverstay, StringComparison.Ordinal))
            return booking.OverstayFeeOutstanding > 0;

        return false;
    }

    private static string AlreadyCapturedMessage(string purpose) => purpose switch
    {
        PaymentChargeResolver.PurposeExtension => "Extension payment already completed",
        PaymentChargeResolver.PurposeOverstay => "Overstay fee already paid",
        _ => "Payment already completed"
    };

    private static string AppliedMessage(string purpose) => purpose switch
    {
        PaymentChargeResolver.PurposeExtension => "Payment verified successfully",
        PaymentChargeResolver.PurposeOverstay => "Overstay fee paid successfully",
        _ => "Payment verified successfully"
    };
}
