using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Models;

namespace ParkingApp.Marketplace.Domain.Interfaces;

public interface IParkingSpaceRepository : IRepository<ParkingSpace>
{
    Task<IEnumerable<ParkingSpace>> SearchAsync(
        string? state = null,
        string? city = null,
        string? address = null,
        double? latitude = null,
        double? longitude = null,
        double? radiusKm = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? parkingType = null,
        string? vehicleType = null,
        string? amenities = null,
        double? minRating = null,
        string? sortBy = null,
        bool sortDescending = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ParkingSpace>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithZoneCodeAsync(string zoneCode, CancellationToken cancellationToken = default);

    Task<IEnumerable<ParkingMapModel>> GetMapCoordinatesAsync(
        string? state = null,
        string? city = null,
        string? address = null,
        double? latitude = null,
        double? longitude = null,
        double? radiusKm = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? parkingType = null,
        string? vehicleType = null,
        string? amenities = null,
        double? minRating = null,
        CancellationToken cancellationToken = default);
}

public interface IBookingRepository : IRepository<Booking>
{
    Task<IEnumerable<Booking>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<Booking?> GetByReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);
    Task<Booking?> GetByAccessPassTokenAsync(string accessPassToken, CancellationToken cancellationToken = default);
    Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<int> GetActiveBookingsCountAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default);
    Task<bool> HasActiveVehicleOverlapAsync(Guid userId, string vehicleNumber, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> IsSlotOccupiedInWindowAsync(Guid parkingSpaceId, int slotNumber, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> HasBlockingBookingsForSpaceAsync(Guid parkingSpaceId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetActiveBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetForecastRelevantBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bookings eligible for LPR entry/exit at a facility for a normalized plate.
    /// </summary>
    Task<IReadOnlyList<Booking>> FindLprCandidatesAsync(
        Guid parkingSpaceId,
        string normalizedLicensePlate,
        LprDirection direction,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>In-progress bookings past EndDateTime that have not been overstay-notified.</summary>
    Task<IReadOnlyList<Booking>> GetOverdueInProgressAsync(
        DateTime asOfUtc,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirmed/InProgress bookings ending within (now, windowEndUtc] that have not been session-reminded.
    /// </summary>
    Task<IReadOnlyList<Booking>> GetEndingSoonForReminderAsync(
        DateTime nowUtc,
        DateTime windowEndUtc,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bookings linked to the given event packages (for sell-through revenue).
    /// </summary>
    Task<IReadOnlyList<Booking>> GetByEventPackageIdsAsync(
        IEnumerable<Guid> eventPackageIds,
        CancellationToken cancellationToken = default);
}

public interface ILprAccessAttemptRepository : IRepository<LprAccessAttempt>
{
}

public interface ILprCameraKeyRepository : IRepository<LprCameraKey>
{
    Task<LprCameraKey?> FindEnabledBySecretHashAsync(string secretHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LprCameraKey>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<bool> KeyIdExistsAsync(string keyId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

public interface ILprPlateRuleRepository : IRepository<LprPlateRule>
{
    Task<IReadOnlyList<LprPlateRule>> GetEnabledByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LprPlateRule>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid parkingSpaceId, string normalizedPlate, LprPlateRuleType ruleType, Guid? excludeId = null, CancellationToken cancellationToken = default);
}

public interface IParkingPassRepository : IRepository<ParkingPass>
{
    Task<IReadOnlyList<ParkingPass>> GetActiveByUserIdAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingPass>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingPass>> GetCandidatePassesForBookingAsync(Guid userId, Guid parkingSpaceId, string? parkingZoneCode, DateTime bookingStartUtc, DateTime bookingEndUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetBookedHoursByDayAsync(Guid parkingPassId, Guid userId, DateTime bookingStartUtc, DateTime bookingEndUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>> GetBookedHoursByDayForPassesAsync(IReadOnlyCollection<Guid> parkingPassIds, Guid userId, DateTime bookingStartUtc, DateTime bookingEndUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
}

public interface IReviewRepository : IRepository<Review>
{
    Task<IEnumerable<Review>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Review>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<double> GetAverageRatingAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
}

public interface IFavoriteRepository : IRepository<Favorite>
{
    Task<IEnumerable<Favorite>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Favorite?> GetByUserAndSpaceAsync(Guid userId, Guid parkingSpaceId, CancellationToken cancellationToken = default);
}

public interface IEvChargingSessionRepository : IRepository<EvChargingSession>
{
    Task<EvChargingSession?> GetByOcppTransactionIdAsync(
        string ocppTransactionId,
        CancellationToken cancellationToken = default);

    Task<EvChargingSession?> GetActiveByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<EvChargingSession?> GetLatestByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}

public interface IEventParkingPackageRepository : IRepository<EventParkingPackage>
{
    Task<IReadOnlyList<EventParkingPackage>> GetByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventParkingPackage>> GetOnSaleAsync(
        DateTime asOfUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<EventParkingPackage?> GetByIdWithSpaceAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventParkingPackage>> GetByVenueEventIdAsync(
        Guid venueEventId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventParkingPackage>> GetByParkingSpaceIdsAsync(
        IEnumerable<Guid> parkingSpaceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default);
}

public interface IParkingAncillaryServiceRepository : IRepository<ParkingAncillaryService>
{
    Task<IReadOnlyList<ParkingAncillaryService>> GetByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkingAncillaryService>> GetByParkingSpaceIdsAsync(
        IEnumerable<Guid> parkingSpaceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<ParkingAncillaryService?> GetByIdWithSpaceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParkingAncillaryService>> GetByIdsForSpaceAsync(
        Guid parkingSpaceId,
        IEnumerable<Guid> serviceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default);
}

public interface IMarketplaceUnitOfWork : IUnitOfWorkTransaction
{
    IParkingSpaceRepository ParkingSpaces { get; }
    IBookingRepository Bookings { get; }
    IParkingPassRepository ParkingPasses { get; }
    IPaymentRepository Payments { get; }
    IReviewRepository Reviews { get; }
    IFavoriteRepository Favorites { get; }
    ILprAccessAttemptRepository LprAccessAttempts { get; }
    ILprCameraKeyRepository LprCameraKeys { get; }
    ILprPlateRuleRepository LprPlateRules { get; }
    IEventParkingPackageRepository EventParkingPackages { get; }
    IEvChargingSessionRepository EvChargingSessions { get; }
    IParkingAncillaryServiceRepository ParkingAncillaryServices { get; }
}

