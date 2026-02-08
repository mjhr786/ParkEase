using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate == null)
            return await _dbSet.CountAsync(cancellationToken);
        return await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        _dbSet.Update(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            entity.IsDeleted = true;
        }
        _dbSet.UpdateRange(entities);
    }

    public IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);
    }
}

public class ParkingSpaceRepository : Repository<ParkingSpace>, IParkingSpaceRepository
{
    public ParkingSpaceRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<ParkingSpace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ParkingSpace>> SearchAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Include(p => p.Owner).Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(state))
            query = query.Where(p => p.State.ToLower() == state.ToLower());

        if (!string.IsNullOrEmpty(city))
            query = query.Where(p => p.City.ToLower().Contains(city.ToLower()));

        if (!string.IsNullOrEmpty(address))
            query = query.Where(p => p.Address.ToLower().Contains(address.ToLower()) || 
                                     p.Title.ToLower().Contains(address.ToLower()));

        if (latitude.HasValue && longitude.HasValue && radiusKm.HasValue)
        {
            // Simple bounding box for SQLite compatibility
            var latDelta = radiusKm.Value / 111.0;
            var lonDelta = radiusKm.Value / (111.0 * Math.Cos(latitude.Value * Math.PI / 180));
            
            query = query.Where(p => 
                p.Latitude >= latitude.Value - latDelta &&
                p.Latitude <= latitude.Value + latDelta &&
                p.Longitude >= longitude.Value - lonDelta &&
                p.Longitude <= longitude.Value + lonDelta);
        }

        if (minPrice.HasValue)
            query = query.Where(p => p.HourlyRate >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.HourlyRate <= maxPrice.Value);

        if (!string.IsNullOrEmpty(parkingType) && Enum.TryParse<ParkingType>(parkingType, out var pt))
            query = query.Where(p => p.ParkingType == pt);

        if (!string.IsNullOrEmpty(vehicleType))
            query = query.Where(p => p.AllowedVehicleTypes == null || 
                                     p.AllowedVehicleTypes.Contains(vehicleType));

        if (!string.IsNullOrEmpty(amenities))
        {
            var amenityList = amenities.Split(',');
            foreach (var amenity in amenityList)
            {
                var a = amenity.Trim();
                query = query.Where(p => p.Amenities != null && p.Amenities.Contains(a));
            }
        }

        if (minRating.HasValue)
            query = query.Where(p => p.AverageRating >= minRating.Value);

        return await query
            .OrderByDescending(p => p.AverageRating)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ParkingSpace>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.ParkingSpace)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.ParkingSpace)
                .ThenInclude(p => p.Owner)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.ParkingSpace)
            .Include(b => b.Payment)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.Payment)
            .Where(b => b.ParkingSpaceId == parkingSpaceId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.ParkingSpace)
            .Include(b => b.Payment)
            .Where(b => b.ParkingSpace.OwnerId == vendorId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetByReferenceAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.User)
            .Include(b => b.ParkingSpace)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingReference == bookingReference, cancellationToken);
    }

    public async Task<bool> HasOverlappingBookingAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(b => 
            b.ParkingSpaceId == parkingSpaceId &&
            b.Status != BookingStatus.Cancelled &&
            b.Status != BookingStatus.Expired &&
            b.Status != BookingStatus.Rejected &&
            ((b.StartDateTime <= startDateTime && b.EndDateTime > startDateTime) ||
             (b.StartDateTime < endDateTime && b.EndDateTime >= endDateTime) ||
             (b.StartDateTime >= startDateTime && b.EndDateTime <= endDateTime)));

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> GetActiveBookingsCountAsync(Guid parkingSpaceId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(b =>
            b.ParkingSpaceId == parkingSpaceId &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Pending) &&
            b.StartDateTime < endDateTime &&
            b.EndDateTime > startDateTime,
            cancellationToken);
    }
}

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);
    }

    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.Booking)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);
    }
}

public class ReviewRepository : Repository<Review>, IReviewRepository
{
    public ReviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Review>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.User)
            .Where(r => r.ParkingSpaceId == parkingSpaceId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Review>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.ParkingSpace)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<double> GetAverageRatingAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        var ratings = await _dbSet
            .Where(r => r.ParkingSpaceId == parkingSpaceId && r.IsApproved)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        return ratings.Any() ? ratings.Average() : 0;
    }
}
