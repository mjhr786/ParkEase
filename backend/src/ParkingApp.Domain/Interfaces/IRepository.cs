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
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ParkingSpace>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
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
