using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Marketplace.Infrastructure.Repositories;
internal sealed class ParkingSpaceRepository : MarketplaceRepository<ParkingSpace>, IParkingSpaceRepository
{
    public ParkingSpaceRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public override async Task<ParkingSpace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            
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
        string? sortBy = null,
        bool sortDescending = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(p => p.IsActive && !p.IsCorporateOnly);
        query = ApplySearchFilters(query, state, city, address, latitude, longitude, radiusKm, minPrice, maxPrice, parkingType, vehicleType, amenities, minRating);

        // Sorting
        if (!string.IsNullOrEmpty(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "price" => sortDescending ? query.OrderByDescending(p => p.HourlyRate) : query.OrderBy(p => p.HourlyRate),
                "rating" => sortDescending ? query.OrderByDescending(p => p.AverageRating) : query.OrderBy(p => p.AverageRating),
                "distance" when latitude.HasValue && longitude.HasValue => 
                    query.OrderBy(p => p.Location != null ? p.Location.Distance(new Point(longitude.Value, latitude.Value) { SRID = 4326 }) : double.MaxValue),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
        }
        else if (latitude.HasValue && longitude.HasValue)
        {
            var orderPoint = new Point(longitude.Value, latitude.Value) { SRID = 4326 };
            query = query.OrderBy(p => p.Location != null ? p.Location.Distance(orderPoint) : double.MaxValue);
        }
        else
        {
            query = query.OrderByDescending(p => p.AverageRating);
        }

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ParkingApp.Marketplace.Domain.Models.ParkingMapModel>> GetMapCoordinatesAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(p => p.IsActive && !p.IsCorporateOnly);
        query = ApplySearchFilters(query, state, city, address, latitude, longitude, radiusKm, minPrice, maxPrice, parkingType, vehicleType, amenities, minRating);

        return await query.Select(p => new ParkingApp.Marketplace.Domain.Models.ParkingMapModel(
            p.Id,
            p.Title,
            p.Address,
            p.City,
            p.Latitude,
            p.Longitude,
            p.HourlyRate,
            p.ImageUrls,
            p.AverageRating,
            p.ParkingType
        ))
        .Take(2000)
        .ToListAsync(cancellationToken);
    }

    private IQueryable<ParkingSpace> ApplySearchFilters(
        IQueryable<ParkingSpace> query,
        string? state,
        string? city,
        string? address,
        double? latitude,
        double? longitude,
        double? radiusKm,
        decimal? minPrice,
        decimal? maxPrice,
        string? parkingType,
        string? vehicleType,
        string? amenities,
        double? minRating)
    {
        if (!string.IsNullOrEmpty(state))
            query = query.Where(p => p.State.ToLower() == state.ToLower());

        if (!string.IsNullOrEmpty(city))
            query = query.Where(p => p.City.ToLower().Contains(city.ToLower()));

        if (!string.IsNullOrEmpty(address))
            query = query.Where(p => p.Address.ToLower().Contains(address.ToLower()) || 
                                     p.Title.ToLower().Contains(address.ToLower()));

        // PostGIS geo-spatial search
        if (latitude.HasValue && longitude.HasValue && radiusKm.HasValue)
        {
            var searchPoint = new Point(longitude.Value, latitude.Value) { SRID = 4326 };
            var radiusMeters = radiusKm.Value * 1000;
            
            query = query.Where(p => p.Location != null && 
                                     p.Location.IsWithinDistance(searchPoint, radiusMeters));
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

        return query;
    }

    public async Task<IEnumerable<ParkingSpace>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithZoneCodeAsync(string zoneCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zoneCode))
            return false;

        var normalized = zoneCode.Trim();
        return await _dbSet.AnyAsync(
            p => p.ZoneCode != null && p.ZoneCode == normalized,
            cancellationToken);
    }
}

internal sealed class BookingRepository : MarketplaceRepository<Booking>, IBookingRepository
{
    public BookingRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public override async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            
            .Include(b => b.ParkingSpace)
                
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .Where(b => b.ParkingSpaceId == parkingSpaceId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .Where(b => b.ParkingSpace.OwnerId == vendorId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetByReferenceAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
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
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Pending || b.Status == BookingStatus.AwaitingPayment) &&
            b.StartDateTime < endDateTime &&
            b.EndDateTime > startDateTime,
            cancellationToken);
    }

    public async Task<bool> HasActiveVehicleOverlapAsync(
        Guid userId,
        string vehicleNumber,
        DateTime startDateTime,
        DateTime endDateTime,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vehicleNumber))
            return false;

        var normalized = vehicleNumber.Trim().ToUpperInvariant();
        var query = _dbSet.Where(b =>
            b.UserId == userId &&
            b.VehicleNumber != null &&
            b.VehicleNumber.ToUpper() == normalized &&
            (b.Status == BookingStatus.Pending
             || b.Status == BookingStatus.AwaitingPayment
             || b.Status == BookingStatus.Confirmed
             || b.Status == BookingStatus.InProgress) &&
            b.StartDateTime < endDateTime &&
            b.EndDateTime > startDateTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> IsSlotOccupiedInWindowAsync(
        Guid parkingSpaceId,
        int slotNumber,
        DateTime startDateTime,
        DateTime endDateTime,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(b =>
            b.ParkingSpaceId == parkingSpaceId &&
            b.SlotNumber == slotNumber &&
            (b.Status == BookingStatus.Pending
             || b.Status == BookingStatus.AwaitingPayment
             || b.Status == BookingStatus.Confirmed
             || b.Status == BookingStatus.InProgress) &&
            b.StartDateTime < endDateTime &&
            b.EndDateTime > startDateTime);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> HasBlockingBookingsForSpaceAsync(
        Guid parkingSpaceId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(b =>
            b.ParkingSpaceId == parkingSpaceId &&
            (b.Status == BookingStatus.Confirmed ||
             b.Status == BookingStatus.InProgress ||
             b.Status == BookingStatus.Pending ||
             b.Status == BookingStatus.AwaitingPayment) &&
            b.EndDateTime > utcNow,
            cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetActiveBookingsForSpacesAsync(IEnumerable<Guid> parkingSpaceIds, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            
            .Where(b => parkingSpaceIds.Contains(b.ParkingSpaceId) &&
                       (b.Status == BookingStatus.Confirmed || 
                        b.Status == BookingStatus.InProgress ||
                        b.Status == BookingStatus.Pending ||
                        b.Status == BookingStatus.AwaitingPayment ||
                        b.Status == BookingStatus.PendingExtension ||
                        b.Status == BookingStatus.AwaitingExtensionPayment) &&
                       b.EndDateTime > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetForecastRelevantBookingsForSpacesAsync(
        IEnumerable<Guid> parkingSpaceIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var parkingIdList = parkingSpaceIds.Distinct().ToList();
        if (parkingIdList.Count == 0)
        {
            return new List<Booking>();
        }

        return await _dbSet
            .AsNoTracking()
            .Where(b => parkingIdList.Contains(b.ParkingSpaceId) &&
                        b.StartDateTime < toUtc &&
                        b.EndDateTime > fromUtc &&
                        b.Status != BookingStatus.Cancelled &&
                        b.Status != BookingStatus.Rejected &&
                        b.Status != BookingStatus.Expired)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class PaymentRepository : MarketplaceRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

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

internal sealed class ReviewRepository : MarketplaceRepository<Review>, IReviewRepository
{
    public ReviewRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<Review>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            
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
        var reviews = await _dbSet
            .Where(r => r.ParkingSpaceId == parkingSpaceId)
            .ToListAsync(cancellationToken);

        return reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
    }
}

