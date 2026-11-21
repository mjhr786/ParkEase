using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Marketplace.Application.Commands.Payments;

// G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??
// Commands
// G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??

public sealed record ProcessPaymentCommand(Guid UserId, CreatePaymentDto Dto) : ICommand<ApiResponse<PaymentResultDto>>;
public sealed record CreatePaymentOrderCommand(
    Guid UserId,
    Guid BookingId,
    /// <summary>When true (or when outstanding overstay exists and booking is not awaiting booking/extension payment), charge overstay fee only.</summary>
    bool? PayOverstayFee = null
) : ICommand<ApiResponse<string>>;
public sealed record VerifyPaymentCommand(Guid UserId, VerifyPaymentDto Dto) : ICommand<ApiResponse<PaymentResultDto>>;
public sealed record ProcessRefundCommand(Guid UserId, RefundRequestDto Dto) : ICommand<ApiResponse<RefundResultDto>>;

// G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??
// Handlers
// G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??G??

internal sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, ApiResponse<PaymentResultDto>>
{
    internal const string MockGateway = "MockGateway";

    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cache;
    private readonly IPaymentCaptureOrchestrator _captureOrchestrator;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentService paymentService,
        ICacheService cache,
        IPaymentCaptureOrchestrator captureOrchestrator,
        ILogger<ProcessPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _cache = cache;
        _captureOrchestrator = captureOrchestrator;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentResultDto>> HandleAsync(ProcessPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);

        if (booking.Status != BookingStatus.AwaitingPayment && booking.Status != BookingStatus.AwaitingExtensionPayment)
            return new ApiResponse<PaymentResultDto>(false, "Booking must be approved by the owner before payment", null);

        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(command.Dto.BookingId, cancellationToken);

        var expectedCapture = PaymentChargeResolver.ResolveExpectedCapture(booking);
        var paymentRequest = new PaymentRequest
        {
            BookingId = command.Dto.BookingId,
            UserId = command.UserId,
            Amount = expectedCapture.Amount,
            Currency = "INR",
            PaymentMethod = command.Dto.PaymentMethod,
            Description = $"Parking booking: {booking.BookingReference}"
        };

        _logger.LogInformation("Processing payment for booking {BookingId}, amount {Amount}", command.Dto.BookingId, expectedCapture.Amount);
        var result = await _paymentService.ProcessPaymentAsync(paymentRequest, cancellationToken);

        if (result.Success)
        {
            if (string.IsNullOrWhiteSpace(result.TransactionId))
                return new ApiResponse<PaymentResultDto>(false, "Payment succeeded without a transaction id", null);

            var capture = await _captureOrchestrator.CaptureAsync(
                new PaymentCaptureCommand(
                    booking.Id,
                    expectedCapture.Purpose,
                    result.TransactionId,
                    result.PaymentGatewayReference,
                    expectedCapture.Amount,
                    "INR",
                    MockGateway,
                    command.Dto.PaymentMethod),
                cancellationToken);

            if (!capture.Success)
                return new ApiResponse<PaymentResultDto>(false, capture.Message ?? "Payment capture failed", null);

            var parking = booking.ParkingSpace ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: parking?.OwnerId,
                cancellationToken);

            return new ApiResponse<PaymentResultDto>(true, null, new PaymentResultDto(
                true, capture.TransactionId, capture.Status, "Payment successful", result.ReceiptUrl));
        }

        if (existingPayment is not { Status: PaymentStatus.Completed })
        {
            var payment = existingPayment ?? Payment.CreatePending(
                command.Dto.BookingId,
                command.UserId,
                expectedCapture.Amount,
                command.Dto.PaymentMethod);

            payment.ApplyGatewayResult(
                result.Status,
                result.TransactionId,
                result.PaymentGatewayReference,
                MockGateway,
                receiptUrl: result.ReceiptUrl,
                failureReason: result.ErrorMessage);

            if (existingPayment == null)
                await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
            else
                _unitOfWork.Payments.Update(payment);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ApiResponse<PaymentResultDto>(false, null, new PaymentResultDto(
            false, result.TransactionId, result.Status, result.ErrorMessage, result.ReceiptUrl));
    }
}

internal sealed class CreatePaymentOrderHandler : ICommandHandler<CreatePaymentOrderCommand, ApiResponse<string>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IStripeCheckoutService _stripeCheckout;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly ICacheService _cache;
    private readonly IUnpaidExpiryService _unpaidExpiry;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IStripeCheckoutService stripeCheckout,
        IPaymentCheckoutStore checkoutStore,
        ICacheService cache,
        IUnpaidExpiryService unpaidExpiry,
        ILogger<CreatePaymentOrderHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _stripeCheckout = stripeCheckout;
        _checkoutStore = checkoutStore;
        _cache = cache;
        _unpaidExpiry = unpaidExpiry;
        _logger = logger;
    }

    public CreatePaymentOrderHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IStripeCheckoutService stripeCheckout,
        IPaymentCheckoutStore checkoutStore,
        ICacheService cache,
        ILogger<CreatePaymentOrderHandler> logger)
        : this(unitOfWork, stripeCheckout, checkoutStore, cache, null!, logger)
    {
        _unpaidExpiry = null!;
    }

    public async Task<ApiResponse<string>> HandleAsync(CreatePaymentOrderCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking == null) return new ApiResponse<string>(false, "Booking not found", null);
        if (booking.UserId != command.UserId) return new ApiResponse<string>(false, "Unauthorized", null);

        if (_unpaidExpiry is not null)
            await _unpaidExpiry.ExpireIfDueAsync(booking, cancellationToken);

        if (!PaymentChargeResolver.TryResolve(booking, command.PayOverstayFee, out var charge, out var error))
            return new ApiResponse<string>(false, error, null);

        var lockKey = PaymentLock.Key(booking.Id, charge.Purpose);
        if (!await _cache.AcquireLockAsync(lockKey, PaymentLock.Ttl, cancellationToken))
        {
            return new ApiResponse<string>(
                false,
                "A payment is already in progress",
                null,
                Code: PaymentErrorCodes.PaymentInProgress);
        }

        try
        {
            foreach (var live in await _checkoutStore.ListLiveOrdersAsync(booking.Id, charge.Purpose, cancellationToken))
            {
                live.Cancel();
                _checkoutStore.Update(live);
            }

            var session = await _stripeCheckout.CreateCardSessionAsync(
                charge.Amount, "INR", charge.Notes, cancellationToken);

            _checkoutStore.Add(PaymentOrder.Create(
                booking.Id,
                charge.Purpose,
                PaymentOrder.RailStripe,
                PaymentOrder.ProviderStripe,
                session.PaymentIntentId,
                charge.Amount,
                "INR",
                PaymentOrderStatus.Created));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<string>(true, null, session.ClientSecret);
        }
        catch (Exception ex) when (UniqueConstraint.IsViolation(ex))
        {
            _unitOfWork.ClearChangeTracker();
            return new ApiResponse<string>(
                false,
                "A payment is already in progress",
                null,
                Code: PaymentErrorCodes.PaymentInProgress);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create payment order for booking {BookingId}", command.BookingId);
            return new ApiResponse<string>(false, "Failed to create payment order", null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}

internal sealed class VerifyPaymentHandler : ICommandHandler<VerifyPaymentCommand, ApiResponse<PaymentResultDto>>
{
    internal const string StripeGateway = "Stripe";

    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cache;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IUpiCheckoutService _upiCheckout;
    private readonly IPaymentCaptureOrchestrator _captureOrchestrator;
    private readonly IUnpaidExpiryService? _unpaidExpiry;
    private readonly ILogger<VerifyPaymentHandler> _logger;

    public VerifyPaymentHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentService paymentService,
        ICacheService cache,
        IPaymentCheckoutStore checkoutStore,
        IUpiCheckoutService upiCheckout,
        IPaymentCaptureOrchestrator captureOrchestrator,
        ILogger<VerifyPaymentHandler> logger,
        IUnpaidExpiryService? unpaidExpiry = null)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _cache = cache;
        _checkoutStore = checkoutStore;
        _upiCheckout = upiCheckout;
        _captureOrchestrator = captureOrchestrator;
        _unpaidExpiry = unpaidExpiry;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentResultDto>> HandleAsync(VerifyPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId, cancellationToken);
        if (booking == null) return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId) return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);

        if (_unpaidExpiry is not null)
            await _unpaidExpiry.ExpireIfDueAsync(booking, cancellationToken);

        var paymentId = command.Dto.RazorpayPaymentId?.Trim();
        var orderId = command.Dto.RazorpayOrderId?.Trim();
        var signature = command.Dto.RazorpaySignature?.Trim();

        // (a) overstay already captured with this provider payment id
        if (!string.IsNullOrWhiteSpace(paymentId)
            && string.Equals(booking.OverstayFeeTransactionId, paymentId, StringComparison.Ordinal))
        {
            return new ApiResponse<PaymentResultDto>(true, "Overstay fee already paid", new PaymentResultDto(
                true, booking.OverstayFeeTransactionId, PaymentStatus.Completed, "Overstay fee already paid", null));
        }

        var order = await FindOrderAsync(orderId, paymentId, cancellationToken);
        var decision = PaymentRailDetector.Detect(order, paymentId, orderId, signature, command.Dto.Rail);
        if (decision.Mismatch)
        {
            return new ApiResponse<PaymentResultDto>(
                false,
                "Payment rail does not match the order",
                null,
                Code: PaymentErrorCodes.RailMismatch);
        }

        var rail = decision.Rail;
        if (rail is null)
        {
            if (!string.IsNullOrWhiteSpace(paymentId)
                && !string.IsNullOrWhiteSpace(orderId)
                && !string.IsNullOrWhiteSpace(signature))
            {
                rail = PaymentRailDetector.Stripe;
            }
            else if (!string.IsNullOrWhiteSpace(paymentId) && !string.IsNullOrWhiteSpace(orderId))
            {
                rail = PaymentRailDetector.Upi;
            }
        }

        if (string.Equals(rail, PaymentRailDetector.Stripe, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(paymentId)
                || string.IsNullOrWhiteSpace(orderId)
                || string.IsNullOrWhiteSpace(signature))
            {
                return new ApiResponse<PaymentResultDto>(false, "Payment verification fields are required", null);
            }
        }
        else if (string.Equals(rail, PaymentRailDetector.Upi, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId))
                return new ApiResponse<PaymentResultDto>(false, "Payment verification fields are required", null);
        }
        else
        {
            return new ApiResponse<PaymentResultDto>(false, "Payment verification fields are required", null);
        }

        var expectedCapture = PaymentChargeResolver.ResolveExpectedCapture(booking);
        var lockKey = PaymentLock.Key(booking.Id, expectedCapture.Purpose);
        if (!await _cache.AcquireLockAsync(lockKey, PaymentLock.WebhookTtl, cancellationToken))
        {
            return new ApiResponse<PaymentResultDto>(
                false,
                "A payment is already in progress",
                null,
                Code: PaymentErrorCodes.PaymentInProgress);
        }

        try
        {
            await _checkoutStore.LockOrderAsync(booking.Id, expectedCapture.Purpose, cancellationToken);

            if (string.Equals(rail, PaymentRailDetector.Upi, StringComparison.Ordinal))
                return await VerifyUpiAsync(booking, paymentId!, orderId!, expectedCapture, cancellationToken);

            return await VerifyStripeAsync(
                booking, paymentId!, orderId!, signature!, expectedCapture, cancellationToken);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }

    private async Task<ApiResponse<PaymentResultDto>> VerifyStripeAsync(
        Booking booking,
        string paymentId,
        string orderId,
        string signature,
        PaymentChargeResolver.Charge expectedCapture,
        CancellationToken cancellationToken)
    {
        var isValid = await _paymentService.VerifyPaymentSignatureAsync(
            paymentId,
            orderId,
            signature,
            cancellationToken,
            new PaymentCaptureCheck(booking.Id, expectedCapture.Amount, "INR", expectedCapture.Purpose));
        if (!isValid)
        {
            _logger.LogWarning("Invalid payment signature for booking {BookingId}", booking.Id);
            return new ApiResponse<PaymentResultDto>(false, "Invalid payment signature", null);
        }

        var capture = await _captureOrchestrator.CaptureAsync(
            new PaymentCaptureCommand(
                booking.Id,
                expectedCapture.Purpose,
                paymentId,
                orderId,
                expectedCapture.Amount,
                "INR",
                StripeGateway,
                PaymentMethod.CreditCard),
            cancellationToken);

        if (capture.NeedsRefund)
        {
            await TryRefundAsync(paymentId, expectedCapture.Amount, StripeGateway, cancellationToken);
            return new ApiResponse<PaymentResultDto>(false, capture.Message ?? "Payment capture failed", null);
        }

        if (!capture.Success)
        {
            _logger.LogWarning(
                "Capture rejected for booking {BookingId} purpose {Purpose} outcome {Outcome}",
                booking.Id,
                expectedCapture.Purpose,
                capture.Outcome);
            return new ApiResponse<PaymentResultDto>(false, capture.Message ?? "Payment capture failed", null);
        }

        await InvalidateAsync(booking, cancellationToken);
        return new ApiResponse<PaymentResultDto>(true, capture.Message, new PaymentResultDto(
            true, capture.TransactionId, capture.Status, capture.Message, capture.ReceiptUrl));
    }

    private async Task<ApiResponse<PaymentResultDto>> VerifyUpiAsync(
        Booking booking,
        string paymentId,
        string orderId,
        PaymentChargeResolver.Charge expectedCapture,
        CancellationToken cancellationToken)
    {
        UpiPaymentSnapshot snapshot;
        try
        {
            using var getCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            getCts.CancelAfter(PaymentLock.WebhookGetTimeout);
            snapshot = await _upiCheckout.GetPaymentAsync(paymentId, getCts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "UPI verify GET failed for booking {BookingId}", booking.Id);
            return new ApiResponse<PaymentResultDto>(false, "Unable to verify UPI payment", null);
        }

        if (!string.Equals(snapshot.Status, "captured", StringComparison.OrdinalIgnoreCase))
            return new ApiResponse<PaymentResultDto>(false, "Payment has not been captured", null);

        var capturedPaymentId = string.IsNullOrWhiteSpace(snapshot.Id) ? paymentId : snapshot.Id;
        var capturedOrderId = snapshot.OrderId ?? orderId;
        var expected = new PaymentCaptureCheck(
            booking.Id, expectedCapture.Amount, "INR", expectedCapture.Purpose);
        if (!_upiCheckout.MatchesExpected(snapshot, expected))
        {
            PaymentObservability.LogMatcherMismatch(
                _logger,
                booking.Id,
                expectedCapture.Purpose,
                PaymentObservability.ToMinorUnits(expectedCapture.Amount),
                snapshot.AmountMinor,
                capturedPaymentId);
            PaymentObservability.LogNeedsRefund(
                _logger,
                booking.Id,
                expectedCapture.Purpose,
                capturedPaymentId,
                PaymentObservability.ReasonMismatch);
            await TryRefundAsync(capturedPaymentId, expectedCapture.Amount, PaymentOrder.ProviderRazorpay, cancellationToken);
            return new ApiResponse<PaymentResultDto>(false, "Payment does not match this booking", null);
        }

        var capture = await _captureOrchestrator.CaptureAsync(
            new PaymentCaptureCommand(
                booking.Id,
                expectedCapture.Purpose,
                capturedPaymentId,
                capturedOrderId,
                expectedCapture.Amount,
                "INR",
                PaymentOrder.ProviderRazorpay,
                PaymentMethod.UPI),
            cancellationToken);

        if (capture.NeedsRefund)
        {
            PaymentObservability.LogNeedsRefund(
                _logger,
                booking.Id,
                expectedCapture.Purpose,
                capturedPaymentId,
                PaymentObservability.CaptureRefundReason(capture.Outcome));
            await TryRefundAsync(capturedPaymentId, expectedCapture.Amount, PaymentOrder.ProviderRazorpay, cancellationToken);
            return new ApiResponse<PaymentResultDto>(false, capture.Message ?? "Payment capture failed", null);
        }

        if (!capture.Success)
        {
            _logger.LogWarning(
                "UPI capture rejected for booking {BookingId} purpose {Purpose} outcome {Outcome}",
                booking.Id,
                expectedCapture.Purpose,
                capture.Outcome);
            return new ApiResponse<PaymentResultDto>(false, capture.Message ?? "Payment capture failed", null);
        }

        await InvalidateAsync(booking, cancellationToken);
        return new ApiResponse<PaymentResultDto>(true, capture.Message, new PaymentResultDto(
            true, capture.TransactionId, capture.Status, capture.Message, capture.ReceiptUrl));
    }

    private async Task TryRefundAsync(
        string providerPaymentId,
        decimal amount,
        string gateway,
        CancellationToken cancellationToken)
    {
        try
        {
            await _paymentService.ProcessRefundAsync(
                new RefundRequest
                {
                    Amount = amount,
                    Reason = "unconfirmable captured charge",
                    GatewayTransactionId = providerPaymentId,
                    PaymentGateway = gateway
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Verify auto-refund failed for {ProviderPaymentId}", providerPaymentId);
        }
    }

    private async Task InvalidateAsync(Booking booking, CancellationToken cancellationToken)
    {
        var parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
        await CacheInvalidation.ForBookingChangeAsync(
            _cache, booking.ParkingSpaceId, booking.UserId, parkingSpace?.OwnerId, cancellationToken);
    }

    private async Task<PaymentOrder?> FindOrderAsync(
        string? orderId,
        string? paymentId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var byRazorpay = await _checkoutStore.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderRazorpay, orderId, cancellationToken);
            if (byRazorpay is not null)
                return byRazorpay;
            var byStripe = await _checkoutStore.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderStripe, orderId, cancellationToken);
            if (byStripe is not null)
                return byStripe;
        }

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            var byPay = await _checkoutStore.FindByProviderPaymentIdAsync(
                PaymentOrder.ProviderRazorpay, paymentId, cancellationToken);
            if (byPay is not null)
                return byPay;
            return await _checkoutStore.FindByProviderPaymentIdAsync(
                PaymentOrder.ProviderStripe, paymentId, cancellationToken);
        }

        return null;
    }
}

internal sealed class ProcessRefundHandler : ICommandHandler<ProcessRefundCommand, ApiResponse<RefundResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ProcessRefundHandler> _logger;

    public ProcessRefundHandler(IMarketplaceUnitOfWork unitOfWork, IPaymentService paymentService, ILogger<ProcessRefundHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<ApiResponse<RefundResultDto>> HandleAsync(ProcessRefundCommand command, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(command.Dto.PaymentId, cancellationToken);
        if (payment == null) return new ApiResponse<RefundResultDto>(false, "Payment not found", null);
        if (payment.UserId != command.UserId) return new ApiResponse<RefundResultDto>(false, "Unauthorized", null);
        if (payment.Status != PaymentStatus.Completed) return new ApiResponse<RefundResultDto>(false, "Cannot refund a non-completed payment", null);
        if (string.IsNullOrWhiteSpace(payment.TransactionId))
            return new ApiResponse<RefundResultDto>(false, "Payment has no gateway transaction id to refund", null);

        var refundRequest = new RefundRequest
        {
            PaymentId = command.Dto.PaymentId,
            Amount = command.Dto.Amount,
            Reason = command.Dto.Reason,
            GatewayTransactionId = payment.TransactionId,
            PaymentGateway = payment.PaymentGateway
        };

        _logger.LogInformation("Processing refund for payment {PaymentId}, amount {Amount}", command.Dto.PaymentId, command.Dto.Amount);
        var result = await _paymentService.ProcessRefundAsync(refundRequest, cancellationToken);

        if (result.Success)
        {
            payment.RecordRefund(result.RefundedAmount, command.Dto.Reason, result.RefundTransactionId);

            _unitOfWork.Payments.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ApiResponse<RefundResultDto>(result.Success, null, new RefundResultDto(
            result.Success, result.RefundTransactionId, result.RefundedAmount,
            result.Success ? "Refund processed successfully" : result.ErrorMessage));
    }
}





