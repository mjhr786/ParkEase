using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;


namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Read-model port for booking list queries. Filters and paging run in the database GÇö
/// handlers should not load full booking aggregate graphs for list endpoints.
/// </summary>
public interface IBookingReadStore
{
    Task<BookingListResultDto> GetUserBookingsAsync(
        Guid userId,
        BookingFilterDto? filter,
        CancellationToken ct = default);

    Task<BookingListResultDto> GetVendorBookingsAsync(
        Guid vendorId,
        BookingFilterDto? filter,
        CancellationToken ct = default);

    Task<BookingListResultDto> GetByParkingSpaceAsync(
        Guid parkingSpaceId,
        BookingFilterDto? filter,
        CancellationToken ct = default);

    Task<int> CountPendingForVendorAsync(Guid vendorId, CancellationToken ct = default);
}
