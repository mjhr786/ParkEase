using Dapper;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<GetParkingByIdHandler> _logger;

    public GetParkingByIdHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<GetParkingByIdHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> HandleAsync(GetParkingByIdQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"parking:{query.ParkingId}";
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
    private readonly IUnitOfWork _unitOfWork;

    public GetOwnerParkingsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<ParkingSpaceDto>>> HandleAsync(GetOwnerParkingsQuery query, CancellationToken cancellationToken = default)
    {
        var parkingSpaces = await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(query.OwnerId, cancellationToken);
        var parkingList = parkingSpaces.ToList();

        // Batch fetch active bookings for all parking spaces
        var parkingIds = parkingList.Select(p => p.Id).ToList();
        var allBookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(parkingIds, cancellationToken);
        var bookingsByParkingId = allBookings.GroupBy(b => b.ParkingSpaceId).ToDictionary(g => g.Key, g => g.ToList());

        var dtos = parkingList.Select(p => 
        {
            var bookings = bookingsByParkingId.GetValueOrDefault(p.Id) ?? new List<Domain.Entities.Booking>();
            return p.ToDtoWithReservations(bookings);
        }).ToList();

        return new ApiResponse<List<ParkingSpaceDto>>(true, null, dtos);
    }
}

public sealed class SearchParkingHandler : IQueryHandler<SearchParkingQuery, ApiResponse<ParkingSearchResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IRoutingService _routing;
    private readonly ILogger<SearchParkingHandler> _logger;

    public SearchParkingHandler(IUnitOfWork unitOfWork, ICacheService cache, IRoutingService routing, ILogger<SearchParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _routing = routing;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSearchResultDto>> HandleAsync(SearchParkingQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.Dto;
        var amenitiesKey = dto.Amenities != null ? string.Join(",", dto.Amenities.OrderBy(a => a)) : "";
        var cacheKey = $"search:{dto.State}:{dto.City}:{dto.Address}:{dto.ParkingType}:{dto.VehicleType}:{dto.MinPrice}:{dto.MaxPrice}:{amenitiesKey}:{dto.Page}:{dto.PageSize}";
        var cached = await _cache.GetAsync<ParkingSearchResultDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSearchResultDto>(true, null, cached);
        }

        _logger.LogInformation("Searching parking spaces: City={City}, Type={ParkingType}", dto.City, dto.ParkingType);

        var parkingSpaces = await _unitOfWork.ParkingSpaces.SearchAsync(
            state: dto.State, city: dto.City, address: dto.Address,
            latitude: dto.Latitude, longitude: dto.Longitude, radiusKm: dto.RadiusKm,
            startDate: dto.StartDateTime, endDate: dto.EndDateTime,
            minPrice: dto.MinPrice, maxPrice: dto.MaxPrice,
            amenities: dto.Amenities != null ? string.Join(",", dto.Amenities) : null,
            minRating: dto.MinRating,
            sortBy: dto.SortBy,
            sortDescending: dto.SortDescending,
            page: dto.Page, pageSize: dto.PageSize,
            cancellationToken: cancellationToken);

        var parkingList = parkingSpaces.ToList();
        var totalCount = await _unitOfWork.ParkingSpaces.CountAsync(p => p.IsActive, cancellationToken);

        // Batch fetch active bookings (N+1 fix)
        var parkingIds = parkingList.Select(p => p.Id).ToList();
        var allBookings = await _unitOfWork.Bookings.GetActiveBookingsForSpacesAsync(parkingIds, cancellationToken);
        var bookingsByParkingId = allBookings.GroupBy(b => b.ParkingSpaceId).ToDictionary(g => g.Key, g => g.ToList());

        // Batch fetch routing data if coordinates provided
        List<(double Distance, int Duration)>? routings = null;
        if (dto.Latitude.HasValue && dto.Longitude.HasValue && parkingList.Any())
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

        // Apply sorting by distance if requested
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
/// Dapper-optimized: selects only the 10 columns needed for map pins.
/// Avoids EF entity materialization and change tracking overhead.
/// </summary>
public sealed class GetMapCoordinatesHandler : IQueryHandler<GetMapCoordinatesQuery, ApiResponse<List<ParkingMapDto>>>
{
    private readonly ISqlConnectionFactory _sql;
    private readonly ICacheService _cache;

    public GetMapCoordinatesHandler(ISqlConnectionFactory sql, ICacheService cache)
    {
        _sql = sql;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ParkingMapDto>>> HandleAsync(GetMapCoordinatesQuery query, CancellationToken cancellationToken = default)
    {
        var dto = query.Dto;
        var amenitiesKey = dto.Amenities != null ? string.Join(",", dto.Amenities.OrderBy(a => a)) : "";
        var cacheKey = $"map:{dto.State}:{dto.City}:{dto.Address}:{dto.ParkingType}:{dto.VehicleType}:{dto.MinPrice}:{dto.MaxPrice}:{dto.RadiusKm}:{dto.Latitude}:{dto.Longitude}:{amenitiesKey}";
        var cached = await _cache.GetAsync<List<ParkingMapDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ParkingMapDto>>(true, null, cached);

        // Build parameterized SQL dynamically
        var sql = new System.Text.StringBuilder();
        var parameters = new DynamicParameters();

        sql.Append("""
            SELECT "Id", "Title", "Address", "City", "Latitude", "Longitude",
                   "HourlyRate", "ImageUrls" AS "ThumbnailUrl", "AverageRating", "ParkingType"
            FROM "ParkingSpaces"
            WHERE "IsActive" = TRUE AND "IsDeleted" = FALSE
            """);

        if (!string.IsNullOrEmpty(dto.State))
        {
            sql.Append(""" AND LOWER("State") = LOWER(@State)""");
            parameters.Add("State", dto.State);
        }
        if (!string.IsNullOrEmpty(dto.City))
        {
            sql.Append(""" AND LOWER("City") LIKE '%' || LOWER(@City) || '%'""");
            parameters.Add("City", dto.City);
        }
        if (!string.IsNullOrEmpty(dto.Address))
        {
            sql.Append(""" AND (LOWER("Address") LIKE '%' || LOWER(@Address) || '%' OR LOWER("Title") LIKE '%' || LOWER(@Address) || '%')""");
            parameters.Add("Address", dto.Address);
        }
        if (dto.Latitude.HasValue && dto.Longitude.HasValue && dto.RadiusKm.HasValue)
        {
            sql.Append(""" AND "Location" IS NOT NULL AND ST_DWithin("Location", ST_SetSRID(ST_MakePoint(@Lng, @Lat), 4326)::geography, @RadiusM)""");
            parameters.Add("Lng", dto.Longitude.Value);
            parameters.Add("Lat", dto.Latitude.Value);
            parameters.Add("RadiusM", dto.RadiusKm.Value * 1000);
        }
        if (dto.MinPrice.HasValue)
        {
            sql.Append(""" AND "HourlyRate" >= @MinPrice""");
            parameters.Add("MinPrice", dto.MinPrice.Value);
        }
        if (dto.MaxPrice.HasValue)
        {
            sql.Append(""" AND "HourlyRate" <= @MaxPrice""");
            parameters.Add("MaxPrice", dto.MaxPrice.Value);
        }
        if (dto.ParkingType.HasValue)
        {
            sql.Append(""" AND "ParkingType" = @ParkingType""");
            parameters.Add("ParkingType", (int)dto.ParkingType.Value);
        }
        if (dto.VehicleType.HasValue)
        {
            sql.Append(""" AND ("AllowedVehicleTypes" IS NULL OR "AllowedVehicleTypes" LIKE '%' || @VehicleType || '%')""");
            parameters.Add("VehicleType", dto.VehicleType.Value.ToString());
        }
        if (dto.MinRating.HasValue)
        {
            sql.Append(""" AND "AverageRating" >= @MinRating""");
            parameters.Add("MinRating", dto.MinRating.Value);
        }
        if (dto.Amenities != null && dto.Amenities.Any())
        {
            for (int i = 0; i < dto.Amenities.Count; i++)
            {
                var paramName = $"Amenity{i}";
                sql.Append($""" AND "Amenities" LIKE '%' || @{paramName} || '%'""");
                parameters.Add(paramName, dto.Amenities[i]);
            }
        }

        sql.Append(" LIMIT 2000");

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<MapRow>(sql.ToString(), parameters);

        var dtos = rows.Select(r => new ParkingMapDto(
            r.Id, r.Title, r.Address, r.City, r.Latitude, r.Longitude, r.HourlyRate,
            ExtractThumbnail(r.ThumbnailUrl),
            r.AverageRating, (ParkingType)r.ParkingType
        )).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(2), cancellationToken);
        return new ApiResponse<List<ParkingMapDto>>(true, null, dtos);
    }

    private static string? ExtractThumbnail(string? imageUrls)
    {
        if (string.IsNullOrEmpty(imageUrls)) return null;
        var commaIndex = imageUrls.IndexOf(',');
        return commaIndex > -1 ? imageUrls[..commaIndex] : imageUrls;
    }

    // Internal row type for Dapper mapping
    private sealed class MapRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal HourlyRate { get; set; }
        public string? ThumbnailUrl { get; set; }
        public double AverageRating { get; set; }
        public int ParkingType { get; set; }
    }
}

