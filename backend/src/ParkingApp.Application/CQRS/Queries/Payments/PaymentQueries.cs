using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Application.CQRS.Queries.Payments;

// ────────────────────────────────────────────────────────────────
// Queries
// ────────────────────────────────────────────────────────────────

public sealed record GetPaymentByIdQuery(Guid PaymentId, Guid UserId) : IQuery<ApiResponse<PaymentDto>>;
public sealed record GetPaymentByBookingIdQuery(Guid BookingId, Guid UserId) : IQuery<ApiResponse<PaymentDto>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class GetPaymentByIdHandler : IQueryHandler<GetPaymentByIdQuery, ApiResponse<PaymentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPaymentByIdHandler> _logger;

    public GetPaymentByIdHandler(IUnitOfWork unitOfWork, ILogger<GetPaymentByIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentDto>> HandleAsync(GetPaymentByIdQuery query, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(query.PaymentId, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Domain.Entities.Payment>(query.PaymentId);
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.Booking.UserId != query.UserId)
        {
            _logger.LogUnauthorizedAccess(query.UserId, $"Payment:{query.PaymentId}");
            return new ApiResponse<PaymentDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<PaymentDto>(true, null, payment.ToDto());
    }
}

public sealed class GetPaymentByBookingIdHandler : IQueryHandler<GetPaymentByBookingIdQuery, ApiResponse<PaymentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPaymentByBookingIdHandler> _logger;

    public GetPaymentByBookingIdHandler(IUnitOfWork unitOfWork, ILogger<GetPaymentByBookingIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentDto>> HandleAsync(GetPaymentByBookingIdQuery query, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByBookingIdAsync(query.BookingId, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Domain.Entities.Payment>(query.BookingId);
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.UserId != query.UserId)
        {
            _logger.LogWarning("Unauthorized access attempt to payment for booking {BookingId}", query.BookingId);
            return new ApiResponse<PaymentDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<PaymentDto>(true, null, payment.ToDto());
    }
}
