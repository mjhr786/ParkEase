using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.CQRS.Commands.Bookings;

/// <summary>
/// Command to create a new booking
/// </summary>
public record CreateBookingCommand(
    Guid UserId,
    Guid ParkingSpaceId,
    DateTime StartDateTime,
    DateTime EndDateTime,
    PricingType PricingType,
    VehicleType VehicleType,
    string? VehicleNumber,
    string? VehicleModel,
    string? DiscountCode
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to cancel a booking
/// </summary>
public record CancelBookingCommand(
    Guid BookingId,
    Guid UserId,
    string Reason
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to approve a booking (vendor)
/// </summary>
public record ApproveBookingCommand(
    Guid BookingId,
    Guid VendorId
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to reject a booking (vendor)
/// </summary>
public record RejectBookingCommand(
    Guid BookingId,
    Guid VendorId,
    string? Reason
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to check in to a booking
/// </summary>
public record CheckInCommand(
    Guid BookingId,
    Guid UserId
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to check out from a booking
/// </summary>
public record CheckOutCommand(
    Guid BookingId,
    Guid UserId
) : ICommand<ApiResponse<BookingDto>>;
