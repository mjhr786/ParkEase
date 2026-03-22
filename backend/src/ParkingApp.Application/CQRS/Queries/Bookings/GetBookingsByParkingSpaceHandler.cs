using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Handlers.Bookings;

public class GetBookingsByParkingSpaceHandler : IQueryHandler<GetBookingsByParkingSpaceQuery, ApiResponse<BookingListResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBookingsByParkingSpaceHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetBookingsByParkingSpaceQuery query, CancellationToken cancellationToken = default)
    {
        // Verify ownership
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != query.VendorId)
        {
            return new ApiResponse<BookingListResultDto>(false, "Unauthorized", null);
        }

        var bookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(query.ParkingSpaceId, cancellationToken);
        
        var bookingList = bookings.Select(b => b.ToDto()).ToList();

        // Apply filters
        var filter = query.Filter;
        if (filter != null)
        {
            if (filter.Status.HasValue)
                bookingList = bookingList.Where(b => b.Status == filter.Status.Value).ToList();
            if (filter.StartDate.HasValue)
                bookingList = bookingList.Where(b => b.StartDateTime >= filter.StartDate.Value).ToList();
            if (filter.EndDate.HasValue)
                bookingList = bookingList.Where(b => b.EndDateTime <= filter.EndDate.Value).ToList();
        }

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 20;

        var result = new BookingListResultDto(
            bookingList.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            bookingList.Count,
            page,
            pageSize,
            (int)Math.Ceiling((double)bookingList.Count / pageSize)
        );

        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}
