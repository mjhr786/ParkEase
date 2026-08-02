using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Marketplace write-side contract for cross-module booking actions.
/// Corporate (and others) cancel/confirm marketplace bookings without using repositories.
/// </summary>
public interface IMarketplaceBookingService
{
    /// <summary>
    /// Cancels a marketplace booking. Returns false if not found or already terminal.
    /// Throws domain exceptions for invalid transitions (caller may map to API errors).
    /// </summary>
    Task<MarketplaceBookingCancelResult> CancelAsync(
        Guid bookingId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a corporate booking without calling SaveChanges.
    /// The caller is responsible for committing the transaction on their UnitOfWork.
    /// </summary>
    Task<MarketplaceBookingCreateResult> StageCorporateBookingAsync(
        StageCorporateBookingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record MarketplaceBookingCancelResult(
    bool Success,
    string Message,
    BookingSnapshot? Booking);

public sealed record StageCorporateBookingRequest(
    Guid UserId,
    Guid ParkingSpaceId,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Amount,
    string? VehicleNumber,
    bool IsVisitor,
    VehicleType VehicleType = VehicleType.Car);

public sealed record MarketplaceBookingCreateResult(
    Guid BookingId,
    string? QrCodeToken);
