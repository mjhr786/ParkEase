using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Marketplace.Application.Queries.Parking;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Queries (Data contracts)
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

public sealed record GetParkingByIdQuery(Guid ParkingId) : IQuery<ApiResponse<ParkingSpaceDto>>;
public sealed record GetOwnerParkingsQuery(Guid OwnerId) : IQuery<ApiResponse<List<ParkingSpaceDto>>>;
public sealed record SearchParkingQuery(ParkingSearchDto Dto) : IQuery<ApiResponse<ParkingSearchResultDto>>;
public sealed record GetMapCoordinatesQuery(ParkingSearchDto Dto) : IQuery<ApiResponse<List<ParkingMapDto>>>;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Handlers
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

internal sealed class GetParkingByIdHandler : IQueryHandler<GetParkingByIdQuery, ApiResponse<ParkingSpaceDto>>
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

internal sealed class GetOwnerParkingsHandler : IQueryHandler<GetOwnerParkingsQuery, ApiResponse<List<ParkingSpaceDto>>>
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
            var bookings = bookingsByParkingId.GetValueOrDefault(p.Id) ?? new List<Booking>();
            return p.ToDtoWithReservations(bookings);
        }).ToList();

        // Short TTL ΓÇö embeds live reservations; invalidation also runs on parking/booking mutations
        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(1), cancellationToken);
        return new ApiResponse<List<ParkingSpaceDto>>(true, null, dtos);
    }
}

internal sealed class SearchParkingHandler : IQueryHandler<SearchParkingQuery, ApiResponse<ParkingSearchResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IParkingReadStore _readStore;
    private readonly ICacheService _cache;
    private readonly IRoutingService _routing;
    private readonly IOptionsMonitor<MarketplaceDiscoveryOptions> _discoveryOptions;
    private readonly IOptionsMonitor<RoutingOptions> _routingOptions;
    private readonly ILogger<SearchParkingHandler> _logger;

    public SearchParkingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IParkingReadStore readStore,
        ICacheService cache,
        IRoutingService routing,
        IOptionsMonitor<MarketplaceDiscoveryOptions> discoveryOptions,
        IOptionsMonitor<RoutingOptions> routingOptions,
        ILogger<SearchParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _readStore = readStore;
        _cache = cache;
        _routing = routing;
        _discoveryOptions = discoveryOptions;
        _routingOptions = routingOptions;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSearchResultDto>> HandleAsync(SearchParkingQuery query, CancellationToken cancellationToken = default)
    {
        var opts = _discoveryOptions.CurrentValue.Search;
        var maxPageSize = Math.Clamp(opts.MaxPageSize, 1, 100);
        var cacheMinutes = Math.Clamp(opts.CacheMinutes, 1, 60);
        // Default true when options missing — preserves historical OSRM-on-search behavior.
        var useOsrmOnSearch = _routingOptions.CurrentValue.UseOsrmOnSearch;

        // Clamp paging before cache key so oversized client PageSize cannot fragment or oversize cache.
        var dto = NormalizeSearchPaging(query.Dto, maxPageSize);
        var amenitiesKey = dto.Amenities != null ? string.Join(",", dto.Amenities.OrderBy(a => a)) : "";
        var cacheKey = CacheKeys.Search(
            dto.State, dto.City, dto.Address, dto.ParkingType, dto.VehicleType,
            dto.MinPrice, dto.MaxPrice, amenitiesKey, dto.Page, dto.PageSize,
            dto.Latitude, dto.Longitude, dto.RadiusKm, dto.MinRating, dto.SortBy, dto.SortDescending,
            useOsrmOnSearch);
        var cached = await _cache.GetAsync<ParkingSearchResultDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSearchResultDto>(true, null, cached);
        }

        _logger.LogDebug("Searching parking spaces: City={City}, Type={ParkingType}, Page={Page}/{PageSize}",
            dto.City, dto.ParkingType, dto.Page, dto.PageSize);

        var parkingList = (await _readStore.SearchAsync(dto, cancellationToken)).ToList();
        var totalCount = await _readStore.CountSearchAsync(dto, cancellationToken);

        // Batch fetch active bookings (N+1 fix) - write-model UoW still used for live reservations
        var parkingIds = parkingList.Select(p => p.Id).ToList();
        var allBookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(parkingIds, cancellationToken);
        var bookingsByParkingId = allBookings.GroupBy(b => b.ParkingSpaceId).ToDictionary(g => g.Key, g => g.ToList());

        List<(double Distance, int Duration)>? routings = null;
        if (dto.Latitude.HasValue && dto.Longitude.HasValue && parkingList.Count > 0)
        {
            var destinations = parkingList.Select(p => (p.Latitude, p.Longitude)).ToList();
            // Default path (UseOsrmOnSearch=true): same as before — OSRM + haversine fallback inside service.
            // Free-tier opt-out: haversine only (no outbound HTTP); still returns DistanceKm / duration for UI sort.
            if (useOsrmOnSearch)
            {
                routings = await _routing.GetBatchRoutingAsync(
                    dto.Latitude.Value, dto.Longitude.Value, destinations, cancellationToken);
            }
            else
            {
                routings = _routing.GetBatchHaversine(
                    dto.Latitude.Value, dto.Longitude.Value, destinations);
            }
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

            var priceAsOf = dto.StartDateTime?.ToUniversalTime() ?? DateTime.UtcNow;
            parkingDtos.Add(parking.ToDtoWithFullDetails(bookings, distance, duration, priceAsOf));
        }

        if (dto.SortBy?.ToLower() == "price")
        {
            parkingDtos = dto.SortDescending
                ? parkingDtos.OrderByDescending(p => p.EffectiveHourlyRate).ToList()
                : parkingDtos.OrderBy(p => p.EffectiveHourlyRate).ToList();
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

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(cacheMinutes), cancellationToken);

        return new ApiResponse<ParkingSearchResultDto>(true, null, result);
    }

    internal static ParkingSearchDto NormalizeSearchPaging(ParkingSearchDto dto, int maxPageSize)
    {
        var page = Math.Max(1, dto.Page);
        var requested = dto.PageSize > 0 ? dto.PageSize : 20;
        var pageSize = Math.Clamp(requested, 1, maxPageSize);
        if (page == dto.Page && pageSize == dto.PageSize)
            return dto;
        return dto with { Page = page, PageSize = pageSize };
    }
}

/// <summary>
/// Map pins via <see cref="IParkingReadStore"/> (Infrastructure Dapper). Caching stays in the handler.
/// </summary>
internal sealed class GetMapCoordinatesHandler : IQueryHandler<GetMapCoordinatesQuery, ApiResponse<List<ParkingMapDto>>>
{
    private readonly IParkingReadStore _readStore;
    private readonly ICacheService _cache;
    private readonly IOptionsMonitor<MarketplaceDiscoveryOptions> _discoveryOptions;

    public GetMapCoordinatesHandler(
        IParkingReadStore readStore,
        ICacheService cache,
        IOptionsMonitor<MarketplaceDiscoveryOptions> discoveryOptions)
    {
        _readStore = readStore;
        _cache = cache;
        _discoveryOptions = discoveryOptions;
    }

    public async Task<ApiResponse<List<ParkingMapDto>>> HandleAsync(GetMapCoordinatesQuery query, CancellationToken cancellationToken = default)
    {
        var mapOpts = _discoveryOptions.CurrentValue.Map;
        var cacheMinutes = Math.Clamp(mapOpts.CacheMinutes, 1, 60);
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

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(cacheMinutes), cancellationToken);
        return new ApiResponse<List<ParkingMapDto>>(true, null, dtos);
    }
}



