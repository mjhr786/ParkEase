using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

internal sealed class UnpaidExpiryService : IUnpaidExpiryService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IUpiCheckoutService _upiCheckout;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentCaptureOrchestrator _orchestrator;
    private readonly ICacheService _cache;
    private readonly IOptions<UpiPaymentsOptions> _upiOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnpaidExpiryService> _logger;

    public UnpaidExpiryService(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        IUpiCheckoutService upiCheckout,
        IPaymentService paymentService,
        IPaymentCaptureOrchestrator orchestrator,
        ICacheService cache,
        IOptions<UpiPaymentsOptions> upiOptions,
        TimeProvider timeProvider,
        ILogger<UnpaidExpiryService> logger)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _upiCheckout = upiCheckout;
        _paymentService = paymentService;
        _orchestrator = orchestrator;
        _cache = cache;
        _upiOptions = upiOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> ExpireIfDueAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        if (booking.Status != BookingStatus.AwaitingPayment)
            return false;

        var orders = await _checkoutStore.ListOrdersForBookingPurposeAsync(
            booking.Id, PaymentChargeResolver.PurposeBooking, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!IsExpiryMember(booking, orders, now, _upiOptions.Value.AwaitingPaymentGraceMinutes))
            return false;

        booking.ExpireUnpaid("unpaid UPI order expired");
        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ExpireAwaitingPaymentsResult> RunJobAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(batchSize, 1, 200);
        var acquired = await _cache.AcquireLockAsync(
            PaymentLock.ExpireAwaitingJob, PaymentLock.ExpireJobTtl, cancellationToken);
        if (!acquired)
        {
            _logger.LogInformation("Job {JobName} is already running. Skipping.", PaymentLock.ExpireAwaitingJob);
            return new ExpireAwaitingPaymentsResult(0, 0, 0, 0);
        }

        var started = Stopwatch.StartNew();
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var grace = Math.Clamp(_upiOptions.Value.AwaitingPaymentGraceMinutes, 0, 24 * 60);

            var expiredBookings = 0;
            var candidates = await _checkoutStore.ListExpiryCandidateBookingsAsync(now, grace, take, cancellationToken);
            foreach (var booking in candidates)
            {
                try
                {
                    var orders = await _checkoutStore.ListOrdersForBookingPurposeAsync(
                        booking.Id, PaymentChargeResolver.PurposeBooking, cancellationToken);
                    if (!IsExpiryMember(booking, orders, now, grace))
                        continue;
                    booking.ExpireUnpaid("unpaid UPI order expired");
                    _unitOfWork.Bookings.Update(booking);
                    expiredBookings++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to expire unpaid booking {BookingId}", booking.Id);
                }
            }

            var expiredOrders = 0;
            var liveExpired = await _checkoutStore.ListLiveExpiredOrdersAsync(now, take, cancellationToken);
            foreach (var order in liveExpired)
            {
                order.Expire();
                _checkoutStore.Update(order);
                expiredOrders++;
            }

            if (expiredBookings > 0 || expiredOrders > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            var (needsRefundDrained, errorsRetried) = await DrainWebhookAsync(now, take, cancellationToken);
            PaymentObservability.LogExpireJobCompleted(
                _logger, expiredBookings, expiredOrders, needsRefundDrained, started.ElapsedMilliseconds);
            return new ExpireAwaitingPaymentsResult(expiredBookings, expiredOrders, needsRefundDrained, errorsRetried);
        }
        finally
        {
            await _cache.ReleaseLockAsync(PaymentLock.ExpireAwaitingJob, CancellationToken.None);
        }
    }

    internal static bool IsExpiryMember(
        Booking booking,
        IReadOnlyList<PaymentOrder> purposeBookingOrders,
        DateTime nowUtc,
        int graceMinutes)
    {
        if (booking.Status != BookingStatus.AwaitingPayment)
            return false;

        var upi = purposeBookingOrders
            .Where(o => string.Equals(o.Rail, PaymentOrder.RailUpi, StringComparison.Ordinal)
                        && string.Equals(o.Purpose, PaymentChargeResolver.PurposeBooking, StringComparison.Ordinal))
            .ToList();
        if (upi.Count == 0)
            return false;

        if (purposeBookingOrders.Any(o =>
                o.IsLive && string.Equals(o.Purpose, PaymentChargeResolver.PurposeBooking, StringComparison.Ordinal)))
            return false;

        var last = upi
            .Where(o => o.ExpiresAtUtc.HasValue)
            .Select(o => o.ExpiresAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        if (last == default)
            return false;

        return nowUtc >= last.AddMinutes(Math.Max(0, graceMinutes));
    }

    private async Task<(int Refunds, int Errors)> DrainWebhookAsync(
        DateTime nowUtc,
        int take,
        CancellationToken cancellationToken)
    {
        var cutoff = nowUtc - PaymentLock.ErrorDrainAge;
        var events = await _checkoutStore.ListRefundDrainAsync(cutoff, take, cancellationToken);
        var refunds = 0;
        var errors = 0;

        foreach (var webhookEvent in events)
        {
            if (webhookEvent.RefundAttemptCount >= PaymentWebhookOutcomes.MaxRefundAttempts)
            {
                webhookEvent.MarkProcessed(PaymentWebhookOutcomes.RefundFailedPermanent, nowUtc);
                _checkoutStore.Update(webhookEvent);
                PaymentObservability.LogRefundPermanentFailure(
                    _logger, webhookEvent.ProviderPaymentId, webhookEvent.RefundAttemptCount);
                continue;
            }

            if (string.Equals(webhookEvent.Outcome, PaymentWebhookOutcomes.Error, StringComparison.Ordinal)
                && webhookEvent.ProcessedAtUtc is null)
            {
                if (!IsRazorpayProvider(webhookEvent.Provider))
                    continue;

                if (await TryRetryErrorAsync(webhookEvent, nowUtc, cancellationToken))
                    errors++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(webhookEvent.ProviderPaymentId) || webhookEvent.Amount is not > 0)
                continue;

            webhookEvent.IncrementRefundAttempt();
            try
            {
                var refund = await _paymentService.ProcessRefundAsync(
                    new RefundRequest
                    {
                        Amount = webhookEvent.Amount.Value,
                        Reason = "unconfirmable captured charge",
                        GatewayTransactionId = webhookEvent.ProviderPaymentId,
                        PaymentGateway = IsStripeProvider(webhookEvent.Provider)
                            ? PaymentOrder.ProviderStripe
                            : PaymentOrder.ProviderRazorpay
                    },
                    cancellationToken);
                if (!refund.Success)
                    throw new InvalidOperationException(refund.ErrorMessage ?? "refund failed");

                webhookEvent.MarkProcessed(PaymentWebhookOutcomes.Refunded, nowUtc);
                _checkoutStore.Update(webhookEvent);
                refunds++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                PaymentObservability.LogRefundFailed(_logger, webhookEvent.ProviderPaymentId, ex);
                if (webhookEvent.RefundAttemptCount >= PaymentWebhookOutcomes.MaxRefundAttempts)
                {
                    webhookEvent.MarkProcessed(PaymentWebhookOutcomes.RefundFailedPermanent, nowUtc);
                    PaymentObservability.LogRefundPermanentFailure(
                        _logger, webhookEvent.ProviderPaymentId, webhookEvent.RefundAttemptCount);
                }
                else
                {
                    webhookEvent.SetOutcome(PaymentWebhookOutcomes.NeedsRefund);
                }

                _checkoutStore.Update(webhookEvent);
            }
        }

        if (events.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (refunds, errors);
    }

    private async Task<bool> TryRetryErrorAsync(
        PaymentWebhookEvent webhookEvent,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.ProviderPaymentId))
            return false;

        try
        {
            using var getCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            getCts.CancelAfter(PaymentLock.WebhookGetTimeout);
            var snapshot = await _upiCheckout.GetPaymentAsync(webhookEvent.ProviderPaymentId, getCts.Token);
            if (!string.Equals(snapshot.Status, "captured", StringComparison.OrdinalIgnoreCase))
            {
                webhookEvent.MarkProcessed(PaymentWebhookOutcomes.IgnoredNotCaptured, nowUtc);
                _checkoutStore.Update(webhookEvent);
                return true;
            }

            var order = await _checkoutStore.FindByProviderPaymentIdAsync(
                PaymentOrder.ProviderRazorpay, snapshot.Id, cancellationToken);
            order ??= await _checkoutStore.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderRazorpay, snapshot.OrderId ?? string.Empty, cancellationToken);
            if (order is null)
            {
                webhookEvent.SetOutcome(PaymentWebhookOutcomes.NeedsRefund);
                _checkoutStore.Update(webhookEvent);
                return true;
            }

            var booking = await _unitOfWork.Bookings.GetByIdAsync(order.BookingId, cancellationToken);
            if (booking is null)
            {
                webhookEvent.SetOutcome(PaymentWebhookOutcomes.NeedsRefund);
                _checkoutStore.Update(webhookEvent);
                return true;
            }

            var capture = await _orchestrator.CaptureAsync(
                new PaymentCaptureCommand(
                    booking.Id,
                    order.Purpose,
                    snapshot.Id,
                    snapshot.OrderId ?? order.ProviderOrderId,
                    order.Amount,
                    order.Currency,
                    PaymentOrder.ProviderRazorpay,
                    PaymentMethod.UPI),
                cancellationToken);

            if (capture.Success)
            {
                webhookEvent.MarkProcessed(PaymentWebhookOutcomes.Captured, nowUtc);
                _checkoutStore.Update(webhookEvent);
                return true;
            }

            if (capture.NeedsRefund)
            {
                webhookEvent.SetRefundTarget(snapshot.Id, order.Amount, order.Currency);
                webhookEvent.SetOutcome(PaymentWebhookOutcomes.NeedsRefund);
                _checkoutStore.Update(webhookEvent);
                return true;
            }

            webhookEvent.SetOutcome(PaymentWebhookOutcomes.Error);
            _checkoutStore.Update(webhookEvent);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error drain retry failed for {ProviderPaymentId}", webhookEvent.ProviderPaymentId);
            return false;
        }
    }

    private static bool IsRazorpayProvider(string? provider) =>
        string.Equals(provider, PaymentWebhookOutcomes.ProviderRazorpay, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, PaymentOrder.ProviderRazorpay, StringComparison.OrdinalIgnoreCase);

    private static bool IsStripeProvider(string? provider) =>
        string.Equals(provider, "stripe", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, PaymentOrder.ProviderStripe, StringComparison.OrdinalIgnoreCase);
}
