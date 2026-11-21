using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.Payments;

public sealed record GetUpiPaymentOrderQuery(Guid UserId, string OrderId) : IQuery<ApiResponse<UpiCheckoutDto>>;

internal sealed class GetUpiPaymentOrderHandler : IQueryHandler<GetUpiPaymentOrderQuery, ApiResponse<UpiCheckoutDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IUnpaidExpiryService? _unpaidExpiry;

    public GetUpiPaymentOrderHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        IUnpaidExpiryService? unpaidExpiry = null)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _unpaidExpiry = unpaidExpiry;
    }

    public async Task<ApiResponse<UpiCheckoutDto>> HandleAsync(
        GetUpiPaymentOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.OrderId))
            return new ApiResponse<UpiCheckoutDto>(false, "Order not found", null);

        var order = await _checkoutStore.FindByProviderOrderIdAsync(
            PaymentOrder.ProviderRazorpay, query.OrderId.Trim(), cancellationToken);
        if (order is null)
            return new ApiResponse<UpiCheckoutDto>(false, "Order not found", null);

        var booking = await _unitOfWork.Bookings.GetByIdAsync(order.BookingId, cancellationToken);
        if (booking is null || booking.UserId != query.UserId)
            return new ApiResponse<UpiCheckoutDto>(false, "Order not found", null);

        if (_unpaidExpiry is not null)
            await _unpaidExpiry.ExpireIfDueAsync(booking, cancellationToken);

        return new ApiResponse<UpiCheckoutDto>(true, null, CreateUpiPaymentOrderHandler.ToDto(order));
    }
}
