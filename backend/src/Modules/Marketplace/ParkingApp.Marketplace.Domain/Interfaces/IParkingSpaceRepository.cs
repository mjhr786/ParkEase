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
    Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<int> GetActiveBookingsCountAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default);
    Task<bool> HasActiveVehicleOverlapAsync(Guid userId, string vehicleNumber, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> IsSlotOccupiedInWindowAsync(Guid parkingSpaceId, int slotNumber, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default);
    Task<bool> HasBlockingBookingsForSpaceAsync(Guid parkingSpaceId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetActiveBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetForecastRelevantBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
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

public interface IMarketplaceUnitOfWork : IUnitOfWorkTransaction
{
    IParkingSpaceRepository ParkingSpaces { get; }
    IBookingRepository Bookings { get; }
    IParkingPassRepository ParkingPasses { get; }
    IPaymentRepository Payments { get; }
    IReviewRepository Reviews { get; }
    IFavoriteRepository Favorites { get; }
}

