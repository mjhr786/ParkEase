using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Application.CQRS.Queries.Parking;

// ────────────────────────────────────────────────────────────────
// Queries (Data contracts)
// ────────────────────────────────────────────────────────────────

public sealed record GetParkingByIdQuery(Guid ParkingId) : IQuery<ApiResponse<ParkingSpaceDto>>;
public sealed record GetOwnerParkingsQuery(Guid OwnerId) : IQuery<ApiResponse<List<ParkingSpaceDto>>>;
public sealed record SearchParkingQuery(ParkingSearchDto Dto) : IQuery<ApiResponse<ParkingSearchResultDto>>;
public sealed record GetMapCoordinatesQuery(ParkingSearchDto Dto) : IQuery<ApiResponse<List<ParkingMapDto>>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class GetParkingByIdHandler : IQueryHandler<GetParkingByIdQuery, ApiResponse<ParkingSpaceDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<GetParkingByIdHandler> _logger;

    public GetParkingByIdHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache, ILogger<GetParkingByIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> HandleAsync(GetParkingByIdQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Parking(query.ParkingId);
        var cached = await _cache.GetAsync<ParkingSpaceDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSpaceDto>(true, null, cached);
        }

        _logger.LogCacheMiss(cacheKey);
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<ParkingSpaceDto>(false, "Parking space not found", null);

        var bookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(new[] { parking.Id }, cancellationToken);
        var dto = parking.ToDtoWithReservations(bookings);
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return new ApiResponse<ParkingSpaceDto>(true, null, dto);
    }
}

public sealed class GetOwnerParkingsHandler : IQueryHandler<GetOwnerParkingsQuery, ApiResponse<List<ParkingSpaceDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetOwnerParkingsHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ParkingSpaceDto>>> HandleAsync(GetOwnerParkingsQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.OwnerParkings(query.OwnerId);
        var cached = await _cache.GetAsync<List<ParkingSpaceDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ParkingSpaceDto>>(true, null, cached);

        var parkingSpaces = await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(query.OwnerId, cancellationToken);
        var parkingList = parkingSpaces.ToList();

        // Batch fetch active bookings for all parking spaces
        var parkingIds = parkingList.Select(p => p.Id).ToList();
        var allBookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(parkingIds, cancellationToken);
        var bookingsByParkingId = allBookings.GroupBy(b => b.ParkingSpaceId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = parkingList.Select(p =>
        {
            var bookings = bookingsByParkingId.GetValueOrDefault(p.Id) ?? new List<Domain.Marketplace.Booking>();
            return p.ToDtoWithReservations(bookings);
        }).ToList();

        // Short TTL — embeds live reservations; invalidation also runs on parking/booking mutations
        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(1), cancellationToken);
        return new ApiResponse<List<ParkingSpaceDto>>(true, null, dtos);
    }
}

public sealed class SearchParkingHandler : IQueryHandler<SearchParkingQuery, ApiResponse<ParkingSearchResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IParkingReadStore _readStore;
    private readonly ICacheService _cache;
    private readonly IRoutingService _routing;
    private readonly ILogger<SearchParkingHandler> _logger;

    public SearchParkingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IParkingReadStore readStore,
        ICacheService cache,
        IRoutingService routing,
        ILogger<SearchParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _readStore = readStore;
        _cache = cache;
        _routing = routing;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSearchResultDto>> HandleAsync(SearchParkingQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.Dto;
        var amenitiesKey = dto.Amenities != null ? string.Join(",", dto.Amenities.OrderBy(a => a)) : "";
        var cacheKey = CacheKeys.Search(
            dto.State, dto.City, dto.Address, dto.ParkingType, dto.VehicleType,
            dto.MinPrice, dto.MaxPrice, amenitiesKey, dto.Page, dto.PageSize);
        var cached = await _cache.GetAsync<ParkingSearchResultDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSearchResultDto>(true, null, cached);
        }

        _logger.LogInformation("Searching parking spaces: City={City}, Type={ParkingType}", dto.City, dto.ParkingType);

        var parkingList = (await _readStore.SearchAsync(dto, cancellationToken)).ToList();
        var totalCount = await _readStore.CountSearchAsync(dto, cancellationToken);

        // Batch fetch active bookings (N+1 fix) — write-model UoW still used for live reservations
        var parkingIds = parkingList.Select(p => p.Id).ToList();
        var allBookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(parkingIds, cancellationToken);
        var bookingsByParkingId = allBookings.GroupBy(b => b.ParkingSpaceId).ToDictionary(g => g.Key, g => g.ToList());

        List<(double Distance, int Duration)>? routings = null;
        if (dto.Latitude.HasValue && dto.Longitude.HasValue && parkingList.Count > 0)
        {
            var destinations = parkingList.Select(p => (p.Latitude, p.Longitude)).ToList();
            routings = await _routing.GetBatchRoutingAsync(dto.Latitude.Value, dto.Longitude.Value, destinations, cancellationToken);
        }

        var parkingDtos = new List<ParkingSpaceDto>();
        for (int i = 0; i < parkingList.Count; i++)
        {
            var parking = parkingList[i];
            var bookings = bookingsByParkingId.GetValueOrDefault(parking.Id) ?? new List<Booking>();
            double? distance = null;
            int? duration = null;

            if (routings != null && i < routings.Count)
            {
                distance = routings[i].Distance;
                duration = routings[i].Duration;
            }

            parkingDtos.Add(parking.ToDtoWithFullDetails(bookings, distance, duration));
        }

        if (dto.SortBy?.ToLower() == "distance" && dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            parkingDtos = dto.SortDescending
                ? parkingDtos.OrderByDescending(p => p.DistanceKm ?? double.MaxValue).ToList()
                : parkingDtos.OrderBy(p => p.DistanceKm ?? double.MaxValue).ToList();
        }

        var result = new ParkingSearchResultDto(
            parkingDtos, totalCount, dto.Page, dto.PageSize,
            (int)Math.Ceiling((double)totalCount / dto.PageSize));

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return new ApiResponse<ParkingSearchResultDto>(true, null, result);
    }
}

/// <summary>
/// Map pins via <see cref="IParkingReadStore"/> (Infrastructure Dapper). Caching stays in the handler.
/// </summary>
public sealed class GetMapCoordinatesHandler : IQueryHandler<GetMapCoordinatesQuery, ApiResponse<List<ParkingMapDto>>>
{
    private readonly IParkingReadStore _readStore;
    private readonly ICacheService _cache;

    public GetMapCoordinatesHandler(IParkingReadStore readStore, ICacheService cache)
    {
        _readStore = readStore;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ParkingMapDto>>> HandleAsync(GetMapCoordinatesQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.Dto;
        var amenitiesKey = dto.Amenities != null ? string.Join(",", dto.Amenities.OrderBy(a => a)) : "";
        var cacheKey = CacheKeys.Map(
            dto.State, dto.City, dto.Address, dto.ParkingType, dto.VehicleType,
            dto.MinPrice, dto.MaxPrice, dto.RadiusKm, dto.Latitude, dto.Longitude, amenitiesKey);
        var cached = await _cache.GetAsync<List<ParkingMapDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ParkingMapDto>>(true, null, cached);

        var pins = await _readStore.GetMapPinsAsync(dto, cancellationToken);
        var dtos = pins.ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(2), cancellationToken);
        return new ApiResponse<List<ParkingMapDto>>(true, null, dtos);
    }
}

