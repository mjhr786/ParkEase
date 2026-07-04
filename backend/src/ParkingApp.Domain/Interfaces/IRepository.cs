using System.Linq.Expressions;
using ParkingApp.Domain.Entities;

namespace ParkingApp.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    void HardDelete(T entity);
    void HardDeleteRange(IEnumerable<T> entities);
    IQueryable<T> Query();
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

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
    Task<IEnumerable<ParkingApp.Domain.Models.ParkingMapModel>> GetMapCoordinatesAsync(
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
    Task<IEnumerable<Booking>> GetActiveBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetForecastRelevantBookingsForSpacesAsync(
        IEnumerable<Guid> parkingSpaceIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

public interface IParkingPassRepository : IRepository<ParkingPass>
{
    Task<IReadOnlyList<ParkingPass>> GetActiveByUserIdAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingPass>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingPass>> GetCandidatePassesForBookingAsync(
        Guid userId,
        Guid parkingSpaceId,
        string? parkingZoneCode,
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<DateOnly, decimal>> GetBookedHoursByDayAsync(
        Guid parkingPassId,
        Guid userId,
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
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

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetByParticipantsAsync(Guid parkingSpaceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IChatMessageRepository : IRepository<ChatMessage>
{
    Task<IEnumerable<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountByConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}

public interface IFavoriteRepository : IRepository<Favorite>
{
    Task<IEnumerable<Favorite>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Favorite?> GetByUserAndSpaceAsync(Guid userId, Guid parkingSpaceId, CancellationToken cancellationToken = default);
}

public interface INotificationRepository : IRepository<Notification>
{
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IDeviceTokenRepository : IRepository<DeviceToken>
{
    Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

// ══════════════════════════════════════════════════════
// CORPORATE MODULE REPOSITORIES
// ══════════════════════════════════════════════════════

public interface ICompanyRepository : IRepository<Entities.Corporate.Company>
{
    Task<Entities.Corporate.Company?> GetWithMembershipsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.Company?> GetWithAllocationsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.Company?> GetFullAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.Company?> GetAggregateForBookingAsync(Guid companyId, Guid userId, Guid allocationId, DateTime bookingStart, DateTime bookingEnd, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.Company?> GetAggregateForInvitationAcceptanceAsync(string invitationToken, Guid userId, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.Company?> GetAggregateByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default);
    Task<bool> IsUserMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
    Task<Entities.Corporate.UserCompanyMembership?> GetMembershipAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByRegistrationNumberAsync(string registrationNumber, CancellationToken cancellationToken = default);
}

public interface ICorporateBookingRepository : IRepository<Entities.Corporate.CorporateBooking>
{
    Task<IEnumerable<Entities.Corporate.CorporateBooking>> GetByCompanyIdAsync(Guid companyId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Corporate.CorporateBooking>> GetByMembershipIdAsync(Guid companyId, Guid membershipId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetMembershipBookingCountForDateAsync(Guid companyId, Guid membershipId, DateOnly date, CancellationToken cancellationToken = default);
    Task<int> GetMembershipBookingCountForWeekAsync(Guid companyId, Guid membershipId, DateOnly weekStart, CancellationToken cancellationToken = default);
    Task<int> GetActiveSharedBookingsCountAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetOccupiedSharedSlotNumbersAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetSharedSlotUsageCountsAsync(Guid companyId, Guid allocationId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(Guid companyId, Guid membershipId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingVehicleBookingAsync(Guid companyId, Guid allocationId, string vehicleNumber, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetRecentBookingCreateCountAsync(Guid companyId, Guid membershipId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<int> GetCompanyBookingCountAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public interface IEmployeeInvitationRepository : IRepository<Entities.Corporate.EmployeeInvitation>
{
    Task<bool> HasPendingInvitationAsync(Guid companyId, string email, CancellationToken cancellationToken = default);
}
