using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;


namespace ParkingApp.Marketplace.Application.Queries.Bookings;

/// <summary>
/// Query to get a booking by ID
/// </summary>
public record GetBookingByIdQuery(
    Guid BookingId,
    Guid UserId
) : IQuery<ApiResponse<BookingDto>>;

/// <summary>
/// Query to get a booking by reference number (caller must be guest or parking owner).
/// </summary>
public record GetBookingByReferenceQuery(
    string Reference,
    Guid UserId
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
    string? DiscountCode,
    Guid? UserId = null,
    bool IncludeEvCharging = false,
    IReadOnlyList<Guid>? AncillaryServiceIds = null
) : IQuery<ApiResponse<PriceBreakdownDto>>;

/// <summary>
/// Query to get the count of pending booking requests for a vendor
/// </summary>
public record GetPendingRequestsCountQuery(
    Guid VendorId
) : IQuery<ApiResponse<int>>;

/// <summary>
/// Query to get all bookings for a specific parking space (vendor only)
/// </summary>
public record GetBookingsByParkingSpaceQuery(
    Guid ParkingSpaceId,
    Guid VendorId,
    BookingFilterDto? Filter
) : IQuery<ApiResponse<BookingListResultDto>>;

/// <summary>Guest (or facility owner) digital access pass for QR display.</summary>
public record GetBookingAccessPassQuery(
    Guid BookingId,
    Guid UserId
) : IQuery<ApiResponse<BookingAccessPassDto>>;

/// <summary>Download Apple Wallet .pkpass for a booking access pass.</summary>
public record GetAppleWalletPassQuery(
    Guid BookingId,
    Guid UserId
) : IQuery<ApiResponse<AppleWalletPassFileDto>>;

/// <summary>Google Wallet save URL (JWT) for a booking access pass.</summary>
public record GetGoogleWalletSaveLinkQuery(
    Guid BookingId,
    Guid UserId
) : IQuery<ApiResponse<GoogleWalletSaveLinkDto>>;

/// <summary>Verify a scanned access-pass token (guest self-check or vendor gate).</summary>
public record VerifyAccessPassQuery(
    string Token,
    Guid? RequesterUserId = null
) : IQuery<ApiResponse<AccessPassVerifyResultDto>>;

