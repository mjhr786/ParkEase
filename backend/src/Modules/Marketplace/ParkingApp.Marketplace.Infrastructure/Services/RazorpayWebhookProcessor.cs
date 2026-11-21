using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal sealed class RazorpayWebhookProcessor : IPaymentWebhookProcessor
{
    private static readonly object DuplicateBody = new { duplicate = true };
    private static readonly object ErrorBody = new { outcome = PaymentWebhookOutcomes.Error };

    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IMarketplaceDbContext _db;
    private readonly IPaymentCaptureOrchestrator _orchestrator;
    private readonly IUpiCheckoutService _upiCheckout;
    private readonly ICacheService _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RazorpayWebhookProcessor> _logger;

    public RazorpayWebhookProcessor(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        IMarketplaceDbContext db,
        IPaymentCaptureOrchestrator orchestrator,
        IUpiCheckoutService upiCheckout,
        ICacheService cache,
        TimeProvider timeProvider,
        ILogger<RazorpayWebhookProcessor> logger)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _db = db;
        _orchestrator = orchestrator;
        _upiCheckout = upiCheckout;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PaymentWebhookProcessResult> ProcessRazorpayAsync(
        byte[] rawBody,
        string payloadSha256,
        string? eventIdHeader,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ParsedWebhook? parsed = null;
        try
        {
            parsed = ParsedWebhook.Parse(rawBody);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        var eventId = NormalizeEventId(eventIdHeader);
        if (string.IsNullOrEmpty(eventId))
        {
            if (parsed is null || string.IsNullOrEmpty(parsed.PaymentId))
                return new PaymentWebhookProcessResult(400, new { error = "event_id_required" });

            eventId = RazorpayWebhookSignature.FallbackEventId(
                PaymentWebhookOutcomes.ProviderRazorpay,
                parsed.EventType ?? string.Empty,
                parsed.PaymentId);
        }

        var webhookEvent = PaymentWebhookEvent.Create(
            PaymentWebhookOutcomes.ProviderRazorpay,
            eventId,
            signatureValid: true,
            payloadSha256);
        _checkoutStore.Add(webhookEvent);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (UniqueConstraint.IsViolation(ex))
        {
            _unitOfWork.ClearChangeTracker();
            webhookEvent = await _checkoutStore.FindWebhookEventAsync(
                PaymentWebhookOutcomes.ProviderRazorpay, eventId, cancellationToken);
            if (webhookEvent is null)
                return Status500();
            if (webhookEvent.ProcessedAtUtc is not null)
                return new PaymentWebhookProcessResult(200, DuplicateBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment webhook insert failed");
            return Status500();
        }

        try
        {
            return await ProcessInsertedAsync(webhookEvent, parsed, startedAtUtc, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Payment webhook processing failed after insert");
            webhookEvent.SetOutcome(PaymentWebhookOutcomes.Error);
            try
            {
                _checkoutStore.Update(webhookEvent);
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Payment webhook failed to persist error outcome");
            }

            return Status500();
        }
    }

    private async Task<PaymentWebhookProcessResult> ProcessInsertedAsync(
        PaymentWebhookEvent webhookEvent,
        ParsedWebhook? parsed,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (parsed is null || !parsed.IsCaptureEvent)
        {
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.IgnoredUnhandled, cancellationToken);
        }

        var lookupId = !string.IsNullOrEmpty(parsed.PaymentId) ? parsed.PaymentId : parsed.OrderId;
        if (string.IsNullOrEmpty(lookupId))
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.IgnoredUnhandled, cancellationToken);

        UpiPaymentSnapshot snapshot;
        try
        {
            using var getCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            getCts.CancelAfter(PaymentLock.WebhookGetTimeout);
            snapshot = await _upiCheckout.GetPaymentAsync(lookupId, getCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            webhookEvent.SetOutcome(PaymentWebhookOutcomes.Error);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Status500();
        }

        var status = snapshot.Status ?? string.Empty;
        if (status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.IgnoredFailed, cancellationToken);
        if (!status.Equals("captured", StringComparison.OrdinalIgnoreCase))
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.IgnoredNotCaptured, cancellationToken);

        var paymentId = !string.IsNullOrEmpty(snapshot.Id) ? snapshot.Id : parsed.PaymentId;
        var orderId = snapshot.OrderId ?? parsed.OrderId;
        if (string.IsNullOrEmpty(paymentId))
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.IgnoredUnhandled, cancellationToken);

        webhookEvent.SetRefundTarget(
            paymentId,
            snapshot.AmountMinor / 100m,
            string.IsNullOrWhiteSpace(snapshot.Currency) ? "INR" : snapshot.Currency);
        _checkoutStore.Update(webhookEvent);

        var order = await ResolveOrderAsync(orderId, paymentId, cancellationToken);
        var bookingId = TryBookingId(snapshot.Notes) ?? order?.BookingId;
        var purpose = TryPurpose(snapshot.Notes) ?? order?.Purpose ?? PaymentChargeResolver.PurposeBooking;

        Booking? booking = null;
        if (bookingId is Guid bid)
            booking = await _unitOfWork.Bookings.GetByIdAsync(bid, cancellationToken);

        if (booking is null || order is null || !MatcherMatches(snapshot, order, booking))
        {
            if (booking is not null && order is not null)
            {
                PaymentObservability.LogMatcherMismatch(
                    _logger,
                    booking.Id,
                    order.Purpose,
                    PaymentObservability.ToMinorUnits(order.Amount),
                    snapshot.AmountMinor,
                    paymentId);
            }

            PaymentObservability.LogNeedsRefund(
                _logger,
                booking?.Id ?? order?.BookingId,
                purpose,
                paymentId,
                PaymentObservability.ReasonMismatch);
            return await EnqueueNeedsRefundAsync(
                webhookEvent,
                PaymentWebhookOutcomes.NeedsRefund,
                cancellationToken);
        }

        var lockKey = PaymentLock.Key(booking.Id, purpose);
        var acquired = await _cache.AcquireLockAsync(lockKey, PaymentLock.WebhookTtl, cancellationToken);
        if (!acquired)
        {
            webhookEvent.SetOutcome(PaymentWebhookOutcomes.Error);
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(
                503,
                new { error = "lock_not_acquired" },
                RazorpayWebhookSignature.RetryAfterSeconds);
        }

        try
        {
            var wasNonLive = order.Status is PaymentOrderStatus.Cancelled
                or PaymentOrderStatus.Expired
                or PaymentOrderStatus.Failed;

            return await ExecuteCaptureStrategyAsync(() =>
                CaptureInTransactionAsync(
                    webhookEvent,
                    booking,
                    purpose,
                    paymentId,
                    orderId,
                    order,
                    wasNonLive,
                    startedAtUtc,
                    cancellationToken));
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, CancellationToken.None);
        }
    }

    private async Task<PaymentWebhookProcessResult> ExecuteCaptureStrategyAsync(
        Func<Task<PaymentWebhookProcessResult>> action)
    {
        if (_db is DbContext dbContext)
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(action);
        }

        return await action();
    }

    private async Task<PaymentWebhookProcessResult> CaptureInTransactionAsync(
        PaymentWebhookEvent webhookEvent,
        Booking booking,
        string purpose,
        string paymentId,
        string? orderId,
        PaymentOrder order,
        bool wasNonLive,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _checkoutStore.LockOrderAsync(booking.Id, purpose, cancellationToken);

            var command = new PaymentCaptureCommand(
                booking.Id,
                purpose,
                paymentId,
                orderId,
                order.Amount,
                order.Currency,
                PaymentOrder.ProviderRazorpay,
                PaymentMethod.UPI);

            var capture = await _orchestrator.CaptureAsync(command, cancellationToken);

            if (capture.Outcome is PaymentCaptureOutcome.AlreadyCaptured)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return await CompleteAsync(
                    webhookEvent,
                    PaymentWebhookOutcomes.CapturedDuplicate,
                    cancellationToken);
            }

            if (capture.NeedsRefund)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                PaymentObservability.LogNeedsRefund(
                    _logger,
                    booking.Id,
                    purpose,
                    paymentId,
                    PaymentObservability.CaptureRefundReason(capture.Outcome));
                return await AutoRefundAsync(
                    webhookEvent, paymentId, order.Amount, order.Currency, startedAtUtc, cancellationToken);
            }

            if (capture.Outcome is PaymentCaptureOutcome.Applied)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                var outcome = wasNonLive
                    ? PaymentWebhookOutcomes.CapturedRevived
                    : PaymentWebhookOutcomes.Captured;
                return await CompleteAsync(webhookEvent, outcome, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            webhookEvent.MarkProcessed(PaymentWebhookOutcomes.Error, UtcNow());
            _checkoutStore.Update(webhookEvent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new PaymentWebhookProcessResult(200, Body(PaymentWebhookOutcomes.Error));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PaymentWebhookProcessResult> AutoRefundAsync(
        PaymentWebhookEvent webhookEvent,
        string paymentId,
        decimal amount,
        string currency,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        var elapsed = UtcNow() - startedAtUtc;
        if (elapsed > PaymentLock.WebhookGetTimeout)
        {
            return await EnqueueNeedsRefundAsync(webhookEvent, PaymentWebhookOutcomes.NeedsRefund, cancellationToken);
        }

        webhookEvent.IncrementRefundAttempt();
        try
        {
            var remaining = PaymentLock.WebhookGetTimeout - elapsed;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            using var refundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            refundCts.CancelAfter(remaining);
            var refund = await _upiCheckout.RefundAsync(paymentId, amount, currency, refundCts.Token);
            if (!refund.Success)
                throw new InvalidOperationException(refund.ErrorMessage ?? "UPI refund failed");
            return await CompleteAsync(webhookEvent, PaymentWebhookOutcomes.Refunded, cancellationToken);
        }
        catch (Exception ex)
        {
            PaymentObservability.LogRefundFailed(_logger, paymentId, ex);
            if (webhookEvent.RefundAttemptCount >= PaymentWebhookOutcomes.MaxRefundAttempts)
            {
                PaymentObservability.LogRefundPermanentFailure(_logger, paymentId, webhookEvent.RefundAttemptCount);
                return await CompleteAsync(
                    webhookEvent, PaymentWebhookOutcomes.RefundFailedPermanent, cancellationToken);
            }

            return await EnqueueNeedsRefundAsync(webhookEvent, PaymentWebhookOutcomes.NeedsRefund, cancellationToken);
        }
    }

    private async Task<PaymentOrder?> ResolveOrderAsync(
        string? orderId,
        string paymentId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(paymentId))
        {
            var byPay = await _checkoutStore.FindByProviderPaymentIdAsync(
                PaymentOrder.ProviderRazorpay, paymentId, cancellationToken);
            if (byPay is not null)
                return byPay;
        }

        if (!string.IsNullOrEmpty(orderId))
        {
            return await _checkoutStore.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderRazorpay, orderId, cancellationToken);
        }

        return null;
    }

    private static bool MatcherMatches(UpiPaymentSnapshot snapshot, PaymentOrder order, Booking booking)
    {
        var expected = new PaymentCaptureCheck(booking.Id, order.Amount, order.Currency, order.Purpose);
        return PaymentCaptureMatcher.Matches(snapshot.AmountMinor, snapshot.Currency, snapshot.Notes, expected);
    }

    private static Guid? TryBookingId(IReadOnlyDictionary<string, string> notes)
    {
        if (notes.TryGetValue("bookingId", out var raw) && Guid.TryParse(raw, out var id))
            return id;
        return null;
    }

    private static string? TryPurpose(IReadOnlyDictionary<string, string> notes) =>
        notes.TryGetValue("purpose", out var purpose) && !string.IsNullOrWhiteSpace(purpose)
            ? purpose
            : null;

    private async Task<PaymentWebhookProcessResult> CompleteAsync(
        PaymentWebhookEvent webhookEvent,
        string outcome,
        CancellationToken cancellationToken)
    {
        webhookEvent.MarkProcessed(outcome, UtcNow());
        _checkoutStore.Update(webhookEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new PaymentWebhookProcessResult(200, Body(outcome));
    }

    private async Task<PaymentWebhookProcessResult> EnqueueNeedsRefundAsync(
        PaymentWebhookEvent webhookEvent,
        string outcome,
        CancellationToken cancellationToken) =>
        await CompleteAsync(webhookEvent, outcome, cancellationToken);

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string? NormalizeEventId(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;
        var trimmed = header.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static object Body(string outcome) => new { outcome };

    private static PaymentWebhookProcessResult Status500() =>
        new(500, ErrorBody);

    private sealed class ParsedWebhook
    {
        public string? EventType { get; init; }
        public string? PaymentId { get; init; }
        public string? OrderId { get; init; }

        public bool IsCaptureEvent =>
            string.Equals(EventType, "payment.captured", StringComparison.OrdinalIgnoreCase)
            || string.Equals(EventType, "order.paid", StringComparison.OrdinalIgnoreCase);

        public static ParsedWebhook Parse(byte[] raw)
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var eventEl) ? eventEl.GetString() : null;

            string? paymentId = null;
            string? orderId = null;
            if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                if (TryEntity(payload, "payment", out var payment))
                {
                    paymentId = ReadId(payment);
                    orderId = ReadString(payment, "order_id");
                }

                if (TryEntity(payload, "order", out var order))
                    orderId ??= ReadId(order);
            }

            return new ParsedWebhook
            {
                EventType = eventType,
                PaymentId = paymentId,
                OrderId = orderId
            };
        }

        private static bool TryEntity(JsonElement payload, string name, out JsonElement entity)
        {
            entity = default;
            if (!payload.TryGetProperty(name, out var wrapper) || wrapper.ValueKind != JsonValueKind.Object)
                return false;
            if (wrapper.TryGetProperty("entity", out var inner) && inner.ValueKind == JsonValueKind.Object)
            {
                entity = inner;
                return true;
            }

            entity = wrapper;
            return true;
        }

        private static string? ReadId(JsonElement entity) => ReadString(entity, "id");

        private static string? ReadString(JsonElement entity, string name) =>
            entity.TryGetProperty(name, out var el) ? el.GetString() : null;
    }
}
