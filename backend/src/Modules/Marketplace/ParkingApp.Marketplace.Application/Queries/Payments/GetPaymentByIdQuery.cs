using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Marketplace.Application.Queries.Payments;

// GöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇ
// Queries
// GöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇ

public sealed record GetPaymentByIdQuery(Guid PaymentId, Guid UserId) : IQuery<ApiResponse<PaymentDto>>;
public sealed record GetPaymentByBookingIdQuery(Guid BookingId, Guid UserId) : IQuery<ApiResponse<PaymentDto>>;

// GöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇ
// Handlers
// GöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇGöÇ

internal sealed class GetPaymentByIdHandler : IQueryHandler<GetPaymentByIdQuery, ApiResponse<PaymentDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<GetPaymentByIdHandler> _logger;

    public GetPaymentByIdHandler(IMarketplaceUnitOfWork unitOfWork, ILogger<GetPaymentByIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentDto>> HandleAsync(GetPaymentByIdQuery query, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(query.PaymentId, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Payment>(query.PaymentId);
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

internal sealed class GetPaymentByBookingIdHandler : IQueryHandler<GetPaymentByBookingIdQuery, ApiResponse<PaymentDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<GetPaymentByBookingIdHandler> _logger;

    public GetPaymentByBookingIdHandler(IMarketplaceUnitOfWork unitOfWork, ILogger<GetPaymentByBookingIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentDto>> HandleAsync(GetPaymentByBookingIdQuery query, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByBookingIdAsync(query.BookingId, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Payment>(query.BookingId);
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
