using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using Stripe;
using PaymentMethod = ParkingApp.Marketplace.Contracts.Enums.PaymentMethod;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal sealed class StripeWebhookProcessor : IStripeWebhookProcessor
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IPaymentCaptureOrchestrator _orchestrator;
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookProcessor> _logger;

    public StripeWebhookProcessor(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        IPaymentCaptureOrchestrator orchestrator,
        IPaymentService paymentService,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<StripeWebhookProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _orchestrator = orchestrator;
        _paymentService = paymentService;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentWebhookProcessResult> ProcessAsync(
        byte[] rawBody,
        string? stripeSignatureHeader,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stripeSignatureHeader))
            return new PaymentWebhookProcessResult(400, new { error = "signature_required" });

        var secret = _configuration["Stripe:WebhookSecret"] ?? string.Empty;
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                Encoding.UTF8.GetString(rawBody),
                stripeSignatureHeader,
                secret,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("PaymentWebhookSignatureInvalid");
            return new PaymentWebhookProcessResult(400, new { error = "invalid_signature" });
        }

        if (!string.Equals(stripeEvent.Type, EventTypes.PaymentIntentSucceeded, StringComparison.Ordinal))
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.IgnoredUnhandled });

        if (stripeEvent.Data.Object is not PaymentIntent intent)
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.IgnoredUnhandled });

        if (!string.Equals(intent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.IgnoredNotCaptured });

        var eventId = string.IsNullOrWhiteSpace(stripeEvent.Id) ? $"stripe_{intent.Id}" : stripeEvent.Id;
        var webhookEvent = PaymentWebhookEvent.Create("stripe", eventId, signatureValid: true, payloadSha256: null);
        webhookEvent.SetRefundTarget(intent.Id, intent.Amount / 100m, intent.Currency?.ToUpperInvariant() ?? "INR");
        _checkoutStore.Add(webhookEvent);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (UniqueConstraint.IsViolation(ex))
        {
            _unitOfWork.ClearChangeTracker();
            return new PaymentWebhookProcessResult(200, new { duplicate = true });
        }

        var notes = intent.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value)
                    ?? new Dictionary<string, string>();
        Guid? bookingId = null;
        if (notes.TryGetValue("bookingId", out var bookingRaw) && Guid.TryParse(bookingRaw, out var parsed))
            bookingId = parsed;

        var order = await _checkoutStore.FindByProviderOrderIdAsync(
            PaymentOrder.ProviderStripe, intent.Id, cancellationToken);
        order ??= await _checkoutStore.FindByProviderPaymentIdAsync(
            PaymentOrder.ProviderStripe, intent.Id, cancellationToken);

        bookingId ??= order?.BookingId;
        var purpose = notes.TryGetValue("purpose", out var p) && !string.IsNullOrWhiteSpace(p)
            ? p
            : order?.Purpose ?? PaymentChargeResolver.PurposeBooking;

        Booking? booking = null;
        if (bookingId is Guid bid)
            booking = await _unitOfWork.Bookings.GetByIdAsync(bid, cancellationToken);

        var expected = booking is null
            ? null
            : new PaymentCaptureCheck(
                booking.Id,
                order?.Amount ?? PaymentChargeResolver.ResolveExpectedCapture(booking).Amount,
                string.IsNullOrWhiteSpace(order?.Currency) ? "INR" : order.Currency,
                purpose);

        if (booking is null
            || expected is null
            || !PaymentCaptureMatcher.Matches(intent.Amount, intent.Currency, notes, expected))
        {
            if (expected is not null)
            {
                PaymentObservability.LogMatcherMismatch(
                    _logger,
                    expected.BookingId,
                    expected.Purpose,
                    PaymentObservability.ToMinorUnits(expected.Amount),
                    intent.Amount,
                    intent.Id);
            }

            PaymentObservability.LogNeedsRefund(
                _logger,
                booking?.Id ?? expected?.BookingId,
                purpose,
                intent.Id,
                PaymentObservability.ReasonMismatch);
            return await RefundOrEnqueueAsync(
                webhookEvent, intent.Id, expected?.Amount ?? intent.Amount / 100m, expected?.Currency ?? "INR", cancellationToken);
        }

        var lockKey = PaymentLock.Key(booking.Id, purpose);
        if (!await _cache.AcquireLockAsync(lockKey, PaymentLock.WebhookTtl, cancellationToken))
        {
            webhookEvent.SetOutcome(PaymentWebhookOutcomes.Error);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(503, new { error = "lock_not_acquired" }, "15");
        }

        try
        {
            await _checkoutStore.LockOrderAsync(booking.Id, purpose, cancellationToken);
            var capture = await _orchestrator.CaptureAsync(
                new PaymentCaptureCommand(
                    booking.Id,
                    purpose,
                    intent.Id,
                    intent.Id,
                    expected.Amount,
                    expected.Currency,
                    PaymentOrder.ProviderStripe,
                    PaymentMethod.CreditCard),
                cancellationToken);

            if (capture.NeedsRefund)
            {
                PaymentObservability.LogNeedsRefund(
                    _logger,
                    booking.Id,
                    purpose,
                    intent.Id,
                    PaymentObservability.CaptureRefundReason(capture.Outcome));
                return await RefundOrEnqueueAsync(
                    webhookEvent, intent.Id, expected.Amount, expected.Currency, cancellationToken);
            }

            if (capture.Success)
            {
                webhookEvent.MarkProcessed(
                    capture.Outcome == PaymentCaptureOutcome.AlreadyCaptured
                        ? PaymentWebhookOutcomes.CapturedDuplicate
                        : PaymentWebhookOutcomes.Captured,
                    DateTime.UtcNow);
                _checkoutStore.Update(webhookEvent);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new PaymentWebhookProcessResult(200, new { outcome = webhookEvent.Outcome });
            }

            webhookEvent.MarkProcessed(PaymentWebhookOutcomes.Error, DateTime.UtcNow);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.Error });
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, CancellationToken.None);
        }
    }

    private async Task<PaymentWebhookProcessResult> RefundOrEnqueueAsync(
        PaymentWebhookEvent webhookEvent,
        string providerPaymentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        webhookEvent.SetRefundTarget(providerPaymentId, amount, currency);
        webhookEvent.IncrementRefundAttempt();
        try
        {
            var refund = await _paymentService.ProcessRefundAsync(
                new RefundRequest
                {
                    Amount = amount,
                    Reason = "unconfirmable captured charge",
                    GatewayTransactionId = providerPaymentId,
                    PaymentGateway = PaymentOrder.ProviderStripe
                },
                cancellationToken);
            if (!refund.Success)
                throw new InvalidOperationException(refund.ErrorMessage ?? "Stripe refund failed");

            webhookEvent.MarkProcessed(PaymentWebhookOutcomes.Refunded, DateTime.UtcNow);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.Refunded });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PaymentObservability.LogRefundFailed(_logger, providerPaymentId, ex);
            webhookEvent.SetOutcome(PaymentWebhookOutcomes.NeedsRefund);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(200, new { outcome = PaymentWebhookOutcomes.NeedsRefund });
        }
    }
}
