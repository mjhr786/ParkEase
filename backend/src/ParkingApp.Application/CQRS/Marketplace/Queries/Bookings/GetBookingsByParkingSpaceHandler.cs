using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Handlers.Bookings;

public class GetBookingsByParkingSpaceHandler : IQueryHandler<GetBookingsByParkingSpaceQuery, ApiResponse<BookingListResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IBookingReadStore _readStore;

    public GetBookingsByParkingSpaceHandler(IMarketplaceUnitOfWork unitOfWork, IBookingReadStore readStore)
    {
        _unitOfWork = unitOfWork;
        _readStore = readStore;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetBookingsByParkingSpaceQuery query, CancellationToken cancellationToken = default)
    {
        // Verify ownership (write model — single key lookup)
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != query.VendorId)
        {
            return new ApiResponse<BookingListResultDto>(false, "Unauthorized", null);
        }

        var result = await _readStore.GetByParkingSpaceAsync(query.ParkingSpaceId, query.Filter, cancellationToken);
        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}
