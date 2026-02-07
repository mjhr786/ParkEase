using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS.Queries.Bookings;

/// <summary>
/// Query to get a booking by ID
/// </summary>
public record GetBookingByIdQuery(
    Guid BookingId,
    Guid UserId
) : IQuery<ApiResponse<BookingDto>>;

/// <summary>
/// Query to get a booking by reference number
/// </summary>
public record GetBookingByReferenceQuery(
    string Reference
) : IQuery<ApiResponse<BookingDto>>;

/// <summary>
/// Query to get all bookings for a user
/// </summary>
public record GetUserBookingsQuery(
    Guid UserId,
    BookingFilterDto? Filter
) : IQuery<ApiResponse<BookingListResultDto>>;

/// <summary>
/// Query to get bookings for a vendor's parking spaces
/// </summary>
public record GetVendorBookingsQuery(
    Guid VendorId,
    BookingFilterDto? Filter
) : IQuery<ApiResponse<BookingListResultDto>>;

/// <summary>
/// Query to calculate price for a booking
/// </summary>
public record CalculatePriceQuery(
    Guid ParkingSpaceId,
    DateTime StartDateTime,
    DateTime EndDateTime,
    int PricingType,
    string? DiscountCode
) : IQuery<ApiResponse<PriceBreakdownDto>>;
