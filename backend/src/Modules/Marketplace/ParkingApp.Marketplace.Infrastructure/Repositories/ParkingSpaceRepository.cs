using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.ValueObjects;
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

    public async Task<(IReadOnlyList<ParkingSpace> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        bool? isActive,
        bool? isVerified,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);
        if (isVerified.HasValue)
            query = query.Where(p => p.IsVerified == isVerified.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(term)
                || p.City.ToLower().Contains(term)
                || p.State.ToLower().Contains(term)
                || p.Address.ToLower().Contains(term)
                || (p.ZoneCode != null && p.ZoneCode.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IEnumerable<ParkingSpace>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        // KD-9: marketplace owner/vendor listings exclude company-owned (corporate-only) inventory.
        // Admin listing APIs use SearchForAdminAsync (unfiltered by IsCorporateOnly).
        return await _dbSet
            .Where(p => p.OwnerId == ownerId && !p.IsCorporateOnly)
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
            .Include(b => b.AncillaryLines)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .Include(b => b.AncillaryLines)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.ParkingSpace)
            .Include(b => b.ParkingPass)
            .Include(b => b.Payment)
            .Include(b => b.AncillaryLines)
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
            .Include(b => b.AncillaryLines)
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
            .Include(b => b.AncillaryLines)
            .Where(b => b.ParkingSpace.OwnerId == vendorId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Booking?> GetByAccessPassTokenAsync(string accessPassToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessPassToken))
            return null;

        var token = accessPassToken.Trim().ToUpperInvariant();
        return await _dbSet
            .Include(b => b.ParkingSpace)
            .FirstOrDefaultAsync(b => b.QRCode != null && b.QRCode == token, cancellationToken);
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

    public async Task<IReadOnlyList<Booking>> FindLprCandidatesAsync(
        Guid parkingSpaceId,
        string normalizedLicensePlate,
        LprDirection direction,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedLicensePlate))
            return Array.Empty<Booking>();

        // Load status/window candidates first; fuzzy plate match in memory (hyphen/space variants).
        IQueryable<Booking> query = _dbSet.Where(b =>
            b.ParkingSpaceId == parkingSpaceId &&
            b.VehicleNumber != null &&
            b.VehicleNumber != "");

        if (direction == LprDirection.Entry)
        {
            var earliest = occurredAtUtc.AddHours(1);
            query = query.Where(b =>
                b.Status == BookingStatus.Confirmed &&
                b.StartDateTime <= earliest &&
                occurredAtUtc < b.EndDateTime);
        }
        else
        {
            query = query.Where(b => b.Status == BookingStatus.InProgress);
        }

        var list = await query
            .Include(b => b.ParkingSpace)
            .ToListAsync(cancellationToken);

        list = list
            .Where(b => LicensePlate.Matches(b.VehicleNumber, normalizedLicensePlate))
            .ToList();

        if (direction == LprDirection.Entry)
        {
            return list
                .Where(b => occurredAtUtc >= b.StartDateTime.AddHours(-1) && occurredAtUtc < b.EndDateTime)
                .OrderBy(b => b.StartDateTime)
                .ToList();
        }

        return list
            .OrderByDescending(b => b.CheckInTime ?? b.StartDateTime)
            .ToList();
    }

    public async Task<IReadOnlyList<Booking>> GetOverdueInProgressAsync(
        DateTime asOfUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        // Includes already-notified stays so fees can increase as overstay continues.
        return await _dbSet
            .Include(b => b.ParkingSpace)
            .Where(b =>
                b.Status == BookingStatus.InProgress &&
                b.EndDateTime < asOfUtc)
            .OrderBy(b => b.EndDateTime)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetEndingSoonForReminderAsync(
        DateTime nowUtc,
        DateTime windowEndUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        if (windowEndUtc <= nowUtc)
            return Array.Empty<Booking>();

        return await _dbSet
            .Include(b => b.ParkingSpace)
            .Where(b =>
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress) &&
                b.SessionEndRemindedAt == null &&
                b.EndDateTime > nowUtc &&
                b.EndDateTime <= windowEndUtc)
            .OrderBy(b => b.EndDateTime)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByEventPackageIdsAsync(
        IEnumerable<Guid> eventPackageIds,
        CancellationToken cancellationToken = default)
    {
        var ids = eventPackageIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<Booking>();

        return await _dbSet
            .AsNoTracking()
            .Where(b => b.EventParkingPackageId != null && ids.Contains(b.EventParkingPackageId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        BookingStatus? status,
        Guid? userId,
        Guid? parkingSpaceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(b => b.ParkingSpace)
            .Include(b => b.Payment)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);
        if (userId.HasValue)
            query = query.Where(b => b.UserId == userId.Value);
        if (parkingSpaceId.HasValue)
            query = query.Where(b => b.ParkingSpaceId == parkingSpaceId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                (b.BookingReference != null && b.BookingReference.ToLower().Contains(term))
                || (b.VehicleNumber != null && b.VehicleNumber.ToLower().Contains(term))
                || (b.ParkingSpace != null && b.ParkingSpace.Title.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}

internal sealed class LprAccessAttemptRepository : MarketplaceRepository<LprAccessAttempt>, ILprAccessAttemptRepository
{
    public LprAccessAttemptRepository(IMarketplaceDbContext context) : base((DbContext)context) { }
}

internal sealed class EvChargingSessionRepository : MarketplaceRepository<EvChargingSession>, IEvChargingSessionRepository
{
    public EvChargingSessionRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<EvChargingSession?> GetByOcppTransactionIdAsync(
        string ocppTransactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ocppTransactionId))
            return null;

        var id = ocppTransactionId.Trim();
        return await _dbSet.FirstOrDefaultAsync(s => s.OcppTransactionId == id, cancellationToken);
    }

    public async Task<EvChargingSession?> GetActiveByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(
            s => s.BookingId == bookingId
                 && (s.Status == EvChargingSessionStatus.Charging
                     || s.Status == EvChargingSessionStatus.Pending),
            cancellationToken);
    }

    public async Task<EvChargingSession?> GetLatestByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.BookingId == bookingId)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

internal sealed class EventParkingPackageRepository : MarketplaceRepository<EventParkingPackage>, IEventParkingPackageRepository
{
    public EventParkingPackageRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<IReadOnlyList<EventParkingPackage>> GetByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(p => p.ParkingSpaceId == parkingSpaceId);
        if (activeOnly)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.EventStartUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventParkingPackage>> GetOnSaleAsync(
        DateTime asOfUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        // Coarse SQL filter; IsOnSale applies AccessEndUtc (late exit) after materialize.
        // EventEndUtc + 1 day covers max LateExitMinutes (24h) without translating AddMinutes in SQL.
        var accessHorizon = asOfUtc.AddDays(-1);
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.ParkingSpace)
            .Where(p =>
                p.IsActive &&
                p.SoldCount < p.TotalSpots &&
                p.SalesStartUtc <= asOfUtc &&
                p.EventEndUtc > accessHorizon &&
                (p.SalesEndUtc == null || p.SalesEndUtc >= asOfUtc))
            .OrderBy(p => p.EventStartUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<EventParkingPackage?> GetByIdWithSpaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.ParkingSpace)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EventParkingPackage>> GetByVenueEventIdAsync(
        Guid venueEventId,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(p => p.ParkingSpace)
            .Where(p => p.VenueEventId == venueEventId);

        if (activeOnly)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.PackagePrice)
            .ThenBy(p => p.ZoneName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventParkingPackage>> GetByParkingSpaceIdsAsync(
        IEnumerable<Guid> parkingSpaceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var ids = parkingSpaceIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<EventParkingPackage>();

        var query = _dbSet
            .AsNoTracking()
            .Include(p => p.ParkingSpace)
            .Where(p => ids.Contains(p.ParkingSpaceId));

        if (activeOnly)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.EventStartUtc)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class LprCameraKeyRepository : MarketplaceRepository<LprCameraKey>, ILprCameraKeyRepository
{
    public LprCameraKeyRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<LprCameraKey?> FindEnabledBySecretHashAsync(string secretHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
            return null;

        var hash = secretHash.Trim().ToUpperInvariant();
        return await _dbSet.FirstOrDefaultAsync(
            k => k.IsEnabled && k.SecretHash == hash,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LprCameraKey>> GetByParkingSpaceIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(k => k.ParkingSpaceId == parkingSpaceId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyIdExistsAsync(string keyId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(k => k.KeyId == keyId);
        if (excludeId.HasValue)
            query = query.Where(k => k.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }
}

internal sealed class LprPlateRuleRepository : MarketplaceRepository<LprPlateRule>, ILprPlateRuleRepository
{
    public LprPlateRuleRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<IReadOnlyList<LprPlateRule>> GetEnabledByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(r => r.ParkingSpaceId == parkingSpaceId && r.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LprPlateRule>> GetByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(r => r.ParkingSpaceId == parkingSpaceId)
            .OrderBy(r => r.RuleType)
            .ThenBy(r => r.LicensePlateNormalized)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid parkingSpaceId,
        string normalizedPlate,
        LprPlateRuleType ruleType,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(r =>
            r.ParkingSpaceId == parkingSpaceId
            && r.LicensePlateNormalized == normalizedPlate
            && r.RuleType == ruleType);

        if (excludeId.HasValue)
            query = query.Where(r => r.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
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

    public async Task<(IReadOnlyList<Payment> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        PaymentStatus? status,
        Guid? userId,
        Guid? bookingId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);
        if (bookingId.HasValue)
            query = query.Where(p => p.BookingId == bookingId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var termLower = term.ToLower();
            if (Guid.TryParse(term, out var id))
            {
                query = query.Where(p =>
                    p.Id == id
                    || p.BookingId == id
                    || p.UserId == id
                    || (p.TransactionId != null && p.TransactionId.ToLower().Contains(termLower))
                    || (p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(termLower)));
            }
            else
            {
                query = query.Where(p =>
                    (p.TransactionId != null && p.TransactionId.ToLower().Contains(termLower))
                    || (p.InvoiceNumber != null && p.InvoiceNumber.ToLower().Contains(termLower)));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
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

internal sealed class ParkingAncillaryServiceRepository
    : MarketplaceRepository<ParkingAncillaryService>, IParkingAncillaryServiceRepository
{
    public ParkingAncillaryServiceRepository(IMarketplaceDbContext context) : base((DbContext)context) { }

    public async Task<IReadOnlyList<ParkingAncillaryService>> GetByParkingSpaceIdAsync(
        Guid parkingSpaceId,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(s => s.ParkingSpaceId == parkingSpaceId);
        if (activeOnly)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingAncillaryService>> GetByParkingSpaceIdsAsync(
        IEnumerable<Guid> parkingSpaceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var ids = parkingSpaceIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ParkingAncillaryService>();

        var query = _dbSet.AsNoTracking().Where(s => ids.Contains(s.ParkingSpaceId));
        if (activeOnly)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.ParkingSpaceId)
            .ThenBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ParkingAncillaryService?> GetByIdWithSpaceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(s => s.ParkingSpace)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingAncillaryService>> GetByIdsForSpaceAsync(
        Guid parkingSpaceId,
        IEnumerable<Guid> serviceIds,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var ids = serviceIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ParkingAncillaryService>();

        var query = _dbSet.Where(s =>
            s.ParkingSpaceId == parkingSpaceId &&
            ids.Contains(s.Id));

        if (activeOnly)
            query = query.Where(s => s.IsActive);

        return await query.ToListAsync(cancellationToken);
    }
}

