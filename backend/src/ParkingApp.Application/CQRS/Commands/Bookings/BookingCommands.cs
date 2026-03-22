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
    int? SlotNumber,
    string? VehicleNumber,
    string? VehicleModel,
    string? VehicleColor,
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

/// <summary>
/// Command to update an existing booking
/// </summary>
public record UpdateBookingCommand(
    Guid BookingId,
    Guid UserId,
    UpdateBookingDto Dto
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Command to extend an existing booking — creates a pending extension request
/// that must be approved by the vendor before payment.
/// </summary>
public record RequestExtensionCommand(
    Guid BookingId,
    Guid UserId,
    DateTime NewEndDateTime
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Vendor approves a pending extension request — moves to AwaitingExtensionPayment.
/// </summary>
public record ApproveExtensionCommand(
    Guid BookingId,
    Guid VendorId
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Vendor rejects a pending extension request — reverts to previous booking status.
/// </summary>
public record RejectExtensionCommand(
    Guid BookingId,
    Guid VendorId,
    string? Reason
) : ICommand<ApiResponse<BookingDto>>;

/// <summary>
/// Called after successful extension payment — updates EndDateTime and confirms the extension.
/// </summary>
public record ConfirmExtensionPaymentCommand(
    Guid BookingId,
    Guid UserId
) : ICommand<ApiResponse<BookingDto>>;
