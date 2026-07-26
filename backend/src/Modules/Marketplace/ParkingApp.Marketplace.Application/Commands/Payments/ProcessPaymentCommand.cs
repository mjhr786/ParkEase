using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
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
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cache;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentService paymentService,
        ICacheService cache,
        ILogger<ProcessPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _cache = cache;
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
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
            return new ApiResponse<PaymentResultDto>(false, "Payment already completed", null);

        var paymentRequest = new PaymentRequest
        {
            BookingId = command.Dto.BookingId,
            UserId = command.UserId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = command.Dto.PaymentMethod,
            Description = $"Parking booking: {booking.BookingReference}"
        };

        _logger.LogInformation("Processing payment for booking {BookingId}, amount {Amount}", command.Dto.BookingId, booking.TotalAmount);
        var result = await _paymentService.ProcessPaymentAsync(paymentRequest, cancellationToken);

        var payment = existingPayment ?? Payment.CreatePending(
            command.Dto.BookingId,
            command.UserId,
            booking.TotalAmount,
            command.Dto.PaymentMethod);

        if (result.Success)
        {
            payment.MarkSucceeded(
                result.TransactionId,
                result.PaymentGatewayReference,
                "MockGateway",
                amount: booking.TotalAmount,
                receiptUrl: result.ReceiptUrl);

            var isExtensionPayment = booking.Status == BookingStatus.AwaitingExtensionPayment;
            // Extension: BookingExtensionConfirmedEvent; regular: BookingConfirmedEvent + PaymentCompletedEvent
            if (isExtensionPayment)
                booking.ConfirmExtension();
            else
            {
                booking.Confirm();
                booking.RecordPaymentCompleted(payment.Id, booking.TotalAmount, "INR", isExtensionPayment: false);
                await BayAssignmentHelper.TryApplyAsync(_unitOfWork, booking, cancellationToken);
            }

            _unitOfWork.Bookings.Update(booking);
        }
        else
        {
            payment.ApplyGatewayResult(
                result.Status,
                result.TransactionId,
                result.PaymentGatewayReference,
                "MockGateway",
                receiptUrl: result.ReceiptUrl,
                failureReason: result.ErrorMessage);
        }

        if (existingPayment == null)
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        else
            _unitOfWork.Payments.Update(payment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (result.Success)
        {
            var parking = booking.ParkingSpace ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: parking?.OwnerId,
                cancellationToken);
        }

        return new ApiResponse<PaymentResultDto>(result.Success, null, new PaymentResultDto(
            result.Success, result.TransactionId, result.Status,
            result.Success ? "Payment successful" : result.ErrorMessage, result.ReceiptUrl));
    }
}

internal sealed class CreatePaymentOrderHandler : ICommandHandler<CreatePaymentOrderCommand, ApiResponse<string>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(IMarketplaceUnitOfWork unitOfWork, IPaymentService paymentService, ILogger<CreatePaymentOrderHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<ApiResponse<string>> HandleAsync(CreatePaymentOrderCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking == null) return new ApiResponse<string>(false, "Booking not found", null);
        if (booking.UserId != command.UserId) return new ApiResponse<string>(false, "Unauthorized", null);

        var payOverstay = command.PayOverstayFee == true
            || (command.PayOverstayFee != false
                && booking.OverstayFeeOutstanding > 0
                && booking.Status is BookingStatus.InProgress or BookingStatus.Completed or BookingStatus.Confirmed);

        decimal amount;
        Dictionary<string, string>? notes;

        if (payOverstay)
        {
            if (booking.OverstayFeeOutstanding <= 0)
                return new ApiResponse<string>(false, "No outstanding overstay fee", null);

            amount = booking.OverstayFeeOutstanding;
            notes = new Dictionary<string, string>
            {
                { "bookingId", booking.Id.ToString() },
                { "purpose", "overstay" },
                { "bookingReference", booking.BookingReference ?? string.Empty }
            };
        }
        else
        {
            if (booking.Status != BookingStatus.AwaitingPayment &&
                booking.Status != BookingStatus.AwaitingExtensionPayment)
                return new ApiResponse<string>(false, "Booking is not awaiting payment", null);

            amount = booking.Status == BookingStatus.AwaitingExtensionPayment
                ? (booking.PendingExtensionAmount ?? booking.TotalAmount)
                : booking.TotalAmount;

            notes = new Dictionary<string, string>
            {
                { "bookingId", booking.Id.ToString() },
                { "purpose", booking.Status == BookingStatus.AwaitingExtensionPayment ? "extension" : "booking" }
            };
        }

        try
        {
            var orderId = await _paymentService.CreateOrderAsync(amount, "INR", notes, cancellationToken);
            return new ApiResponse<string>(true, null, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment order for booking {BookingId}", command.BookingId);
            return new ApiResponse<string>(false, "Failed to create payment order", null);
        }
    }
}

internal sealed class VerifyPaymentHandler : ICommandHandler<VerifyPaymentCommand, ApiResponse<PaymentResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ICacheService _cache;
    private readonly ILogger<VerifyPaymentHandler> _logger;

    public VerifyPaymentHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentService paymentService,
        ICacheService cache,
        ILogger<VerifyPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentResultDto>> HandleAsync(VerifyPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId, cancellationToken);
        if (booking == null) return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId) return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);

        var isExtensionPayment = booking.Status == BookingStatus.AwaitingExtensionPayment;
        var isOverstayPayment = !isExtensionPayment
            && booking.Status is not BookingStatus.AwaitingPayment
            && booking.OverstayFeeOutstanding > 0;

        // Idempotent overstay: already paid this PI
        if (isOverstayPayment
            && !string.IsNullOrWhiteSpace(command.Dto.RazorpayPaymentId)
            && string.Equals(booking.OverstayFeeTransactionId, command.Dto.RazorpayPaymentId, StringComparison.Ordinal))
        {
            return new ApiResponse<PaymentResultDto>(true, "Overstay fee already paid", new PaymentResultDto(
                true, booking.OverstayFeeTransactionId, PaymentStatus.Completed, "Overstay fee already paid", null));
        }

        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(command.Dto.BookingId, cancellationToken);
        if (!isOverstayPayment && existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
        {
            // Idempotent recovery path: finalize extension if still awaiting extension payment.
            if (isExtensionPayment)
            {
                booking.ConfirmExtension();
                _unitOfWork.Bookings.Update(booking);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var parkingForExtension = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
                await CacheInvalidation.ForBookingChangeAsync(
                    _cache, booking.ParkingSpaceId, booking.UserId, parkingForExtension?.OwnerId, cancellationToken);
                return new ApiResponse<PaymentResultDto>(true, "Extension payment already completed", new PaymentResultDto(
                    true, existingPayment.TransactionId, PaymentStatus.Completed, "Extension confirmed", existingPayment.ReceiptUrl));
            }

            return new ApiResponse<PaymentResultDto>(true, "Payment already completed", new PaymentResultDto(
                true, existingPayment.TransactionId, PaymentStatus.Completed, "Payment already completed", existingPayment.ReceiptUrl));
        }

        var isValid = await _paymentService.VerifyPaymentSignatureAsync(
            command.Dto.RazorpayPaymentId, command.Dto.RazorpayOrderId, command.Dto.RazorpaySignature, cancellationToken);
        if (!isValid)
        {
            _logger.LogWarning("Invalid payment signature for booking {BookingId}", command.Dto.BookingId);
            return new ApiResponse<PaymentResultDto>(false, "Invalid payment signature", null);
        }

        // ── Overstay fee payment (does not replace primary booking Payment) ──
        if (isOverstayPayment)
        {
            var outstanding = booking.OverstayFeeOutstanding;
            try
            {
                booking.MarkOverstayFeePaid(
                    outstanding,
                    command.Dto.RazorpayPaymentId,
                    DateTime.UtcNow);
            }
            catch (DomainException ex)
            {
                return new ApiResponse<PaymentResultDto>(false, ex.Message, null);
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var parkingForOverstay = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            await CacheInvalidation.ForBookingChangeAsync(
                _cache, booking.ParkingSpaceId, booking.UserId, parkingForOverstay?.OwnerId, cancellationToken);

            _logger.LogInformation(
                "Overstay fee paid for booking {BookingId}, amount {Amount}, remaining {Remaining}",
                booking.Id, outstanding, booking.OverstayFeeOutstanding);

            return new ApiResponse<PaymentResultDto>(true, "Overstay fee paid successfully", new PaymentResultDto(
                true, command.Dto.RazorpayPaymentId, PaymentStatus.Completed, "Overstay fee paid successfully", null));
        }

        var paymentAmount = isExtensionPayment
            ? (booking.PendingExtensionAmount ?? booking.TotalAmount)
            : booking.TotalAmount;

        Payment payment;
        bool isNewPayment;
        if (existingPayment != null)
        {
            payment = existingPayment;
            isNewPayment = false;
        }
        else
        {
            payment = Payment.CreatePending(
                command.Dto.BookingId,
                command.UserId,
                paymentAmount,
                PaymentMethod.CreditCard);
            isNewPayment = true;
        }

        payment.MarkSucceeded(
            command.Dto.RazorpayPaymentId,
            command.Dto.RazorpayOrderId,
            "Razorpay",
            amount: paymentAmount);

        // Extension: BookingExtensionConfirmedEvent; regular: BookingConfirmed + PaymentCompleted events
        if (isExtensionPayment)
            booking.ConfirmExtension();
        else
        {
            booking.Confirm();
            booking.RecordPaymentCompleted(payment.Id, paymentAmount, "INR", isExtensionPayment: false);
            await BayAssignmentHelper.TryApplyAsync(_unitOfWork, booking, cancellationToken);
        }

        _unitOfWork.Bookings.Update(booking);

        if (isNewPayment)
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        else
            _unitOfWork.Payments.Update(payment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
        await CacheInvalidation.ForBookingChangeAsync(
            _cache, booking.ParkingSpaceId, booking.UserId, parkingSpace?.OwnerId, cancellationToken);

        return new ApiResponse<PaymentResultDto>(true, "Payment verified successfully", new PaymentResultDto(
            true, payment.TransactionId, PaymentStatus.Completed, "Payment verified successfully", null));
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

        var refundRequest = new RefundRequest { PaymentId = command.Dto.PaymentId, Amount = command.Dto.Amount, Reason = command.Dto.Reason };

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





