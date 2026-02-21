using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Payments;

// ────────────────────────────────────────────────────────────────
// Commands
// ────────────────────────────────────────────────────────────────

public sealed record ProcessPaymentCommand(Guid UserId, CreatePaymentDto Dto) : ICommand<ApiResponse<PaymentResultDto>>;
public sealed record CreatePaymentOrderCommand(Guid UserId, Guid BookingId) : ICommand<ApiResponse<string>>;
public sealed record VerifyPaymentCommand(Guid UserId, VerifyPaymentDto Dto) : ICommand<ApiResponse<PaymentResultDto>>;
public sealed record ProcessRefundCommand(Guid UserId, RefundRequestDto Dto) : ICommand<ApiResponse<RefundResultDto>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class ProcessPaymentHandler : ICommandHandler<ProcessPaymentCommand, ApiResponse<PaymentResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessPaymentHandler> _logger;

    public ProcessPaymentHandler(IUnitOfWork unitOfWork, IPaymentService paymentService,
        INotificationService notificationService, IEmailService emailService, ILogger<ProcessPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentResultDto>> HandleAsync(ProcessPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);

        if (booking.Status != BookingStatus.AwaitingPayment)
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

        var payment = existingPayment ?? new Payment
        {
            BookingId = command.Dto.BookingId,
            UserId = command.UserId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = command.Dto.PaymentMethod
        };

        payment.Status = result.Status;
        payment.TransactionId = result.TransactionId;
        payment.PaymentGatewayReference = result.PaymentGatewayReference;
        payment.PaymentGateway = "MockGateway";
        payment.ReceiptUrl = result.ReceiptUrl;

        if (result.Success)
        {
            payment.PaidAt = DateTime.UtcNow;
            payment.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            booking.Status = BookingStatus.Confirmed;
            _unitOfWork.Bookings.Update(booking);
        }
        else
        {
            payment.FailureReason = result.ErrorMessage;
        }

        if (existingPayment == null)
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        else
            _unitOfWork.Payments.Update(payment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (result.Success)
        {
            await NotifyPaymentAsync(booking, command.UserId, cancellationToken);
        }

        return new ApiResponse<PaymentResultDto>(result.Success, null, new PaymentResultDto(
            result.Success, result.TransactionId, result.Status,
            result.Success ? "Payment successful" : result.ErrorMessage, result.ReceiptUrl));
    }

    private async Task NotifyPaymentAsync(Booking booking, Guid payerId, CancellationToken cancellationToken)
    {
        try
        {
            var parkingSpace = booking.ParkingSpace ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            if (parkingSpace == null) return;

            var payer = await _unitOfWork.Users.GetByIdAsync(payerId, cancellationToken);
            var payerName = payer?.FirstName ?? "A user";

            await _notificationService.NotifyUserAsync(parkingSpace.OwnerId,
                new NotificationDto("payment.completed", "Payment Received",
                    $"{payerName} has completed payment for booking {booking.BookingReference}",
                    new { BookingId = booking.Id, BookingReference = booking.BookingReference, Amount = booking.TotalAmount }),
                cancellationToken);

            // Email receipt to user
            if (payer?.Email != null)
            {
                await _emailService.SendEmailAsync(payer.Email,
                    $"Payment Receipt: {booking.BookingReference}",
                    $"<p>Hello {payer.FirstName},</p><p>Thank you for your payment of <strong>₹{booking.TotalAmount}</strong> for booking {booking.BookingReference}.</p><p>Your booking is now <strong>Confirmed</strong>.</p>");
            }

            // Email notification to owner
            var owner = await _unitOfWork.Users.GetByIdAsync(parkingSpace.OwnerId, cancellationToken);
            if (owner?.Email != null)
            {
                await _emailService.SendEmailAsync(owner.Email,
                    $"Payment Received: {booking.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p><p>You have received a payment of <strong>₹{booking.TotalAmount}</strong> from {payerName} for booking {booking.BookingReference}.</p>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification for booking {BookingId}", booking.Id);
        }
    }
}

public sealed class CreatePaymentOrderHandler : ICommandHandler<CreatePaymentOrderCommand, ApiResponse<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<CreatePaymentOrderHandler> _logger;

    public CreatePaymentOrderHandler(IUnitOfWork unitOfWork, IPaymentService paymentService, ILogger<CreatePaymentOrderHandler> logger)
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
        if (booking.Status != BookingStatus.AwaitingPayment) return new ApiResponse<string>(false, "Booking is not awaiting payment", null);

        try
        {
            var orderId = await _paymentService.CreateOrderAsync(booking.TotalAmount, "INR", null, cancellationToken);
            return new ApiResponse<string>(true, null, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment order for booking {BookingId}", command.BookingId);
            return new ApiResponse<string>(false, "Failed to create payment order", null);
        }
    }
}

public sealed class VerifyPaymentHandler : ICommandHandler<VerifyPaymentCommand, ApiResponse<PaymentResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<VerifyPaymentHandler> _logger;

    public VerifyPaymentHandler(IUnitOfWork unitOfWork, IPaymentService paymentService,
        INotificationService notificationService, IEmailService emailService, ILogger<VerifyPaymentHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentResultDto>> HandleAsync(VerifyPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId, cancellationToken);
        if (booking == null) return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId) return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);

        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(command.Dto.BookingId, cancellationToken);
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
        {
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

        var payment = existingPayment ?? new Payment
        {
            BookingId = command.Dto.BookingId,
            UserId = command.UserId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = PaymentMethod.CreditCard
        };

        payment.Status = PaymentStatus.Completed;
        payment.TransactionId = command.Dto.RazorpayPaymentId;
        payment.PaymentGatewayReference = command.Dto.RazorpayOrderId;
        payment.PaymentGateway = "Razorpay";
        payment.PaidAt = DateTime.UtcNow;
        payment.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        booking.Status = BookingStatus.Confirmed;
        _unitOfWork.Bookings.Update(booking);

        if (existingPayment == null)
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        else
            _unitOfWork.Payments.Update(payment);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notifications
        try
        {
            var parkingSpace = booking.ParkingSpace ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            if (parkingSpace != null)
            {
                var payer = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
                var payerName = payer?.FirstName ?? "A user";

                await _notificationService.NotifyUserAsync(parkingSpace.OwnerId,
                    new NotificationDto("payment.completed", "Payment Received",
                        $"{payerName} has completed payment for booking {booking.BookingReference}",
                        new { BookingId = booking.Id, BookingReference = booking.BookingReference, Amount = booking.TotalAmount }),
                    cancellationToken);

                if (payer?.Email != null)
                {
                    await _emailService.SendEmailAsync(payer.Email,
                        $"Payment Receipt: {booking.BookingReference}",
                        $"<p>Hello {payer.FirstName},</p><p>Thank you for your payment of <strong>₹{booking.TotalAmount}</strong> for booking {booking.BookingReference}.</p><p>Your booking is now <strong>Confirmed</strong>.</p>");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification for booking {BookingId}", booking.Id);
        }

        return new ApiResponse<PaymentResultDto>(true, "Payment verified successfully", new PaymentResultDto(
            true, payment.TransactionId, PaymentStatus.Completed, "Payment verified successfully", null));
    }
}

public sealed class ProcessRefundHandler : ICommandHandler<ProcessRefundCommand, ApiResponse<RefundResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<ProcessRefundHandler> _logger;

    public ProcessRefundHandler(IUnitOfWork unitOfWork, IPaymentService paymentService, ILogger<ProcessRefundHandler> logger)
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
            payment.Status = command.Dto.Amount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartialRefund;
            payment.RefundAmount = (payment.RefundAmount ?? 0) + result.RefundedAmount;
            payment.RefundReason = command.Dto.Reason;
            payment.RefundTransactionId = result.RefundTransactionId;
            payment.RefundedAt = DateTime.UtcNow;

            _unitOfWork.Payments.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ApiResponse<RefundResultDto>(result.Success, null, new RefundResultDto(
            result.Success, result.RefundTransactionId, result.RefundedAmount,
            result.Success ? "Refund processed successfully" : result.ErrorMessage));
    }
}
