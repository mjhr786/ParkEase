using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Common;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Admin;

public sealed record AdminListPaymentsQuery(
    string? Search,
    PaymentStatus? Status,
    Guid? UserId,
    Guid? BookingId,
    int Page = 1,
    int PageSize = 25) : IQuery<ApiResponse<AdminPaymentPageDto>>;

public sealed record AdminGetPaymentQuery(Guid PaymentId) : IQuery<ApiResponse<AdminPaymentDetailDto>>;

public sealed record AdminProcessRefundCommand(
    Guid ActorAdminUserId,
    string ActorEmail,
    Guid PaymentId,
    string Reason,
    decimal? Amount,
    string? IpAddress,
    string? UserAgent) : ICommand<ApiResponse<AdminPaymentDetailDto>>;

internal sealed class AdminListPaymentsHandler : IQueryHandler<AdminListPaymentsQuery, ApiResponse<AdminPaymentPageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminListPaymentsHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminPaymentPageDto>> HandleAsync(
        AdminListPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, 100);

        var (items, total) = await _unitOfWork.Payments.SearchForAdminAsync(
            query.Search,
            query.Status,
            query.UserId,
            query.BookingId,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(p => new AdminPaymentListItemDto(
            p.Id,
            p.BookingId,
            p.UserId,
            p.Amount,
            p.Currency,
            p.Status,
            p.PaymentMethod,
            p.TransactionId,
            p.RefundAmount,
            p.PaidAt,
            p.CreatedAt)).ToList();

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new ApiResponse<AdminPaymentPageDto>(
            true,
            null,
            new AdminPaymentPageDto(dtos, total, page, pageSize, totalPages));
    }
}

internal sealed class AdminGetPaymentHandler : IQueryHandler<AdminGetPaymentQuery, ApiResponse<AdminPaymentDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminGetPaymentHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminPaymentDetailDto>> HandleAsync(
        AdminGetPaymentQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(query.PaymentId, cancellationToken);
        if (payment is null)
            return new ApiResponse<AdminPaymentDetailDto>(false, "Payment not found", null);

        return new ApiResponse<AdminPaymentDetailDto>(true, null, ToDetail(payment));
    }

    internal static AdminPaymentDetailDto ToDetail(Domain.Entities.Payment p)
    {
        var remaining = Math.Max(0, p.Amount - (p.RefundAmount ?? 0));
        return new(
            p.Id,
            p.BookingId,
            p.UserId,
            p.Amount,
            p.Currency,
            p.Status,
            p.PaymentMethod,
            p.TransactionId,
            p.PaymentGateway,
            p.InvoiceNumber,
            p.RefundAmount,
            p.RefundReason,
            p.RefundTransactionId,
            p.PaidAt,
            p.RefundedAt,
            p.CreatedAt,
            remaining);
    }
}

internal sealed class AdminProcessRefundHandler
    : ICommandHandler<AdminProcessRefundCommand, ApiResponse<AdminPaymentDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IAdminAudit _audit;
    private readonly ILogger<AdminProcessRefundHandler> _logger;

    public AdminProcessRefundHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentService paymentService,
        IAdminAudit audit,
        ILogger<AdminProcessRefundHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ApiResponse<AdminPaymentDetailDto>> HandleAsync(
        AdminProcessRefundCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return new ApiResponse<AdminPaymentDetailDto>(false, "Reason is required", null);

        var reason = command.Reason.Trim();
        if (reason.Length > 500)
            return new ApiResponse<AdminPaymentDetailDto>(false, "Reason must be at most 500 characters", null);

        var payment = await _unitOfWork.Payments.GetByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null)
            return new ApiResponse<AdminPaymentDetailDto>(false, "Payment not found", null);

        if (payment.Status is not (PaymentStatus.Completed or PaymentStatus.PartialRefund))
            return new ApiResponse<AdminPaymentDetailDto>(
                false,
                $"Cannot refund payment in {payment.Status} status",
                null);

        if (string.IsNullOrWhiteSpace(payment.TransactionId))
            return new ApiResponse<AdminPaymentDetailDto>(
                false,
                "Payment has no gateway transaction id to refund",
                null);

        var remaining = payment.Amount - (payment.RefundAmount ?? 0);
        if (remaining <= 0)
            return new ApiResponse<AdminPaymentDetailDto>(false, "Payment has no remaining refundable balance", null);

        var amount = command.Amount is > 0 ? command.Amount.Value : remaining;
        if (amount > remaining)
            return new ApiResponse<AdminPaymentDetailDto>(
                false,
                $"Refund amount exceeds remaining balance ({remaining:0.00})",
                null);

        var refundRequest = new RefundRequest
        {
            PaymentId = payment.Id,
            Amount = amount,
            Reason = reason,
            GatewayTransactionId = payment.TransactionId
        };

        _logger.LogInformation(
            "Admin {ActorId} refunding payment {PaymentId} amount {Amount}",
            command.ActorAdminUserId,
            payment.Id,
            amount);

        var result = await _paymentService.ProcessRefundAsync(refundRequest, cancellationToken);
        if (!result.Success)
        {
            return new ApiResponse<AdminPaymentDetailDto>(
                false,
                result.ErrorMessage ?? "Gateway refund failed",
                AdminGetPaymentHandler.ToDetail(payment));
        }

        try
        {
            payment.RecordRefund(result.RefundedAmount, $"[Admin] {reason}", result.RefundTransactionId);
            _unitOfWork.Payments.Update(payment);

            _audit.Stage(new AdminAuditEntry(
                command.ActorAdminUserId,
                command.ActorEmail,
                "Payment.AdminRefund",
                "Payment",
                payment.Id,
                JsonSerializer.Serialize(new
                {
                    reason,
                    amount = result.RefundedAmount,
                    refundTransactionId = result.RefundTransactionId,
                    bookingId = payment.BookingId,
                    previousStatus = PaymentStatus.Completed.ToString() // approximate; PartialRefund also allowed
                }),
                command.IpAddress,
                command.UserAgent));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<AdminPaymentDetailDto>(
                true,
                "Refund processed",
                AdminGetPaymentHandler.ToDetail(payment));
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<AdminPaymentDetailDto>(ex);
        }
    }
}
