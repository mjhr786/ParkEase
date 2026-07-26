using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.EvCharging;

public sealed record GetEvChargingSessionByBookingQuery(
    Guid BookingId,
    Guid ActorUserId,
    bool ActorIsAdmin = false
) : IQuery<ApiResponse<EvChargingSessionDto>>;

internal sealed class GetEvChargingSessionByBookingHandler
    : IQueryHandler<GetEvChargingSessionByBookingQuery, ApiResponse<EvChargingSessionDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public GetEvChargingSessionByBookingHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<EvChargingSessionDto>> HandleAsync(
        GetEvChargingSessionByBookingQuery query,
        CancellationToken cancellationToken = default)
    {
        var booking = await _uow.Bookings.GetByIdAsync(query.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<EvChargingSessionDto>(false, "Booking not found", null);

        var space = await _uow.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
        if (!query.ActorIsAdmin
            && query.ActorUserId != booking.UserId
            && (space is null || query.ActorUserId != space.OwnerId))
        {
            return new ApiResponse<EvChargingSessionDto>(false, "Unauthorized", null);
        }

        var session = await _uow.EvChargingSessions.GetLatestByBookingIdAsync(query.BookingId, cancellationToken);
        if (session is null)
            return new ApiResponse<EvChargingSessionDto>(false, "No EV charge session for this booking", null);

        return new ApiResponse<EvChargingSessionDto>(true, null, session.ToDto());
    }
}
