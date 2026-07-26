using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Marketplace.Infrastructure.ReadModel.Parking;

internal sealed class ParkingReadStore : IParkingReadStore
{
    private readonly IMarketplaceDbContext _db;
    private readonly ISqlConnectionFactory _sql;
    private readonly IOptionsMonitor<MarketplaceDiscoveryOptions> _discoveryOptions;

    public ParkingReadStore(
        IMarketplaceDbContext db,
        ISqlConnectionFactory sql,
        IOptionsMonitor<MarketplaceDiscoveryOptions> discoveryOptions)
    {
        _db = db;
        _sql = sql;
        _discoveryOptions = discoveryOptions;
    }

    public async Task<IReadOnlyList<ParkingSpace>> SearchAsync(ParkingSearchDto criteria, CancellationToken ct = default)
    {
        // Parity with SearchParkingHandler - IParkingSpaceRepository.SearchAsync call site
        // (parkingType / vehicleType historically not passed from the handler).
        var amenities = criteria.Amenities != null ? string.Join(",", criteria.Amenities) : null;
        var maxPageSize = Math.Clamp(_discoveryOptions.CurrentValue.Search.MaxPageSize, 1, 100);

        var query = _db.ParkingSpaces
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsCorporateOnly);

        query = ApplySearchFilters(
            query,
            criteria.State,
            criteria.City,
            criteria.Address,
            criteria.Latitude,
            criteria.Longitude,
            criteria.RadiusKm,
            criteria.MinPrice,
            criteria.MaxPrice,
            parkingType: null,
            vehicleType: null,
            amenities,
            criteria.MinRating,
            criteria.HasEvCharging,
            ResolveListingCategory(criteria));

        var sortBy = criteria.SortBy;
        var sortDescending = criteria.SortDescending;
        var latitude = criteria.Latitude;
        var longitude = criteria.Longitude;

        if (!string.IsNullOrEmpty(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "price" => sortDescending
                    ? query.OrderByDescending(p => p.HourlyRate)
                    : query.OrderBy(p => p.HourlyRate),
                "rating" => sortDescending
                    ? query.OrderByDescending(p => p.AverageRating)
                    : query.OrderBy(p => p.AverageRating),
                "distance" when latitude.HasValue && longitude.HasValue =>
                    query.OrderBy(p => p.Location != null
                        ? p.Location.Distance(new Point(longitude.Value, latitude.Value) { SRID = 4326 })
                        : double.MaxValue),
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

        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize > 0 ? criteria.PageSize : 20, 1, maxPageSize);

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveAsync(CancellationToken ct = default)
    {
        return await _db.ParkingSpaces.CountAsync(p => p.IsActive, ct);
    }

    public async Task<int> CountSearchAsync(ParkingSearchDto criteria, CancellationToken ct = default)
    {
        var amenities = criteria.Amenities != null ? string.Join(",", criteria.Amenities) : null;

        var query = _db.ParkingSpaces
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsCorporateOnly);

        query = ApplySearchFilters(
            query,
            criteria.State,
            criteria.City,
            criteria.Address,
            criteria.Latitude,
            criteria.Longitude,
            criteria.RadiusKm,
            criteria.MinPrice,
            criteria.MaxPrice,
            parkingType: null,
            vehicleType: null,
            amenities,
            criteria.MinRating,
            criteria.HasEvCharging,
            ResolveListingCategory(criteria));

        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingMapDto>> GetMapPinsAsync(ParkingSearchDto criteria, CancellationToken ct = default)
    {
        var sql = new StringBuilder();
        var parameters = new DynamicParameters();

        // Parity with marketplace search: exclude company-only inventory from public map.
        // Project first image URL only (split_part) so map payloads stay small.
        sql.Append("""
            SELECT "Id", "Title", "Address", "City", "Latitude", "Longitude",
                   "HourlyRate",
                   CASE
                     WHEN "ImageUrls" IS NULL OR BTRIM("ImageUrls") = '' THEN NULL
                     ELSE split_part("ImageUrls", ',', 1)
                   END AS "ThumbnailUrl",
                   "AverageRating", "ParkingType", "ListingCategory", "InstantBook",
                   "TotalSpots", "AvailableSpots", "IsDynamicPricingEnabled",
                   "DynamicMinMultiplier", "DynamicMaxMultiplier",
                   "PeakHourMultiplier", "WeekendMultiplier", "TimeZoneId"
            FROM "ParkingSpaces"
            WHERE "IsActive" = TRUE AND "IsDeleted" = FALSE AND "IsCorporateOnly" = FALSE
            """);

        if (!string.IsNullOrEmpty(criteria.State))
        {
            sql.Append(""" AND LOWER("State") = LOWER(@State)""");
            parameters.Add("State", criteria.State);
        }
        if (!string.IsNullOrEmpty(criteria.City))
        {
            sql.Append(""" AND LOWER("City") LIKE '%' || LOWER(@City) || '%'""");
            parameters.Add("City", criteria.City);
        }
        if (!string.IsNullOrEmpty(criteria.Address))
        {
            sql.Append(""" AND (LOWER("Address") LIKE '%' || LOWER(@Address) || '%' OR LOWER("Title") LIKE '%' || LOWER(@Address) || '%')""");
            parameters.Add("Address", criteria.Address);
        }
        if (criteria.Latitude.HasValue && criteria.Longitude.HasValue && criteria.RadiusKm.HasValue)
        {
            sql.Append(""" AND "Location" IS NOT NULL AND ST_DWithin("Location", ST_SetSRID(ST_MakePoint(@Lng, @Lat), 4326)::geography, @RadiusM)""");
            parameters.Add("Lng", criteria.Longitude.Value);
            parameters.Add("Lat", criteria.Latitude.Value);
            parameters.Add("RadiusM", criteria.RadiusKm.Value * 1000);
        }
        if (criteria.MinPrice.HasValue)
        {
            sql.Append(""" AND "HourlyRate" >= @MinPrice""");
            parameters.Add("MinPrice", criteria.MinPrice.Value);
        }
        if (criteria.MaxPrice.HasValue)
        {
            sql.Append(""" AND "HourlyRate" <= @MaxPrice""");
            parameters.Add("MaxPrice", criteria.MaxPrice.Value);
        }
        if (criteria.ParkingType.HasValue)
        {
            sql.Append(""" AND "ParkingType" = @ParkingType""");
            parameters.Add("ParkingType", (int)criteria.ParkingType.Value);
        }
        if (criteria.VehicleType.HasValue)
        {
            sql.Append(""" AND ("AllowedVehicleTypes" IS NULL OR "AllowedVehicleTypes" LIKE '%' || @VehicleType || '%')""");
            parameters.Add("VehicleType", criteria.VehicleType.Value.ToString());
        }
        if (criteria.MinRating.HasValue)
        {
            sql.Append(""" AND "AverageRating" >= @MinRating""");
            parameters.Add("MinRating", criteria.MinRating.Value);
        }
        if (criteria.Amenities != null && criteria.Amenities.Count > 0)
        {
            for (int i = 0; i < criteria.Amenities.Count; i++)
            {
                var amenity = criteria.Amenities[i]?.Trim() ?? string.Empty;
                if (string.Equals(amenity, "EV_Charging", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(amenity, "EV Charging", StringComparison.OrdinalIgnoreCase))
                {
                    sql.Append(""" AND ("HasEvCharging" = TRUE OR "Amenities" LIKE '%EV%')""");
                    continue;
                }

                var paramName = $"Amenity{i}";
                sql.Append($""" AND "Amenities" LIKE '%' || @{paramName} || '%'""");
                parameters.Add(paramName, amenity);
            }
        }

        if (criteria.HasEvCharging == true)
            sql.Append(""" AND "HasEvCharging" = TRUE""");

        var listingCategory = ResolveListingCategory(criteria);
        if (listingCategory.HasValue)
        {
            sql.Append(""" AND "ListingCategory" = @ListingCategory""");
            parameters.Add("ListingCategory", (int)listingCategory.Value);
        }

        var maxPins = Math.Clamp(_discoveryOptions.CurrentValue.Map.MaxPins, 50, 2000);
        sql.Append(" LIMIT @MaxPins");
        parameters.Add("MaxPins", maxPins);

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<MapRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct));

        var asOf = criteria.StartDateTime?.ToUniversalTime() ?? DateTime.UtcNow;
        return rows.Select(r =>
        {
            var dyn = DynamicPricingCalculator.Calculate(
                r.HourlyRate,
                r.IsDynamicPricingEnabled,
                r.TotalSpots,
                r.AvailableSpots,
                asOf,
                r.DynamicMinMultiplier,
                r.DynamicMaxMultiplier,
                r.PeakHourMultiplier,
                r.WeekendMultiplier,
                timeZoneId: r.TimeZoneId);
            return new ParkingMapDto(
                r.Id,
                r.Title,
                r.Address,
                r.City,
                r.Latitude,
                r.Longitude,
                r.HourlyRate,
                string.IsNullOrWhiteSpace(r.ThumbnailUrl) ? null : r.ThumbnailUrl.Trim(),
                r.AverageRating,
                (ParkingType)r.ParkingType,
                (ListingCategory)r.ListingCategory,
                r.InstantBook,
                dyn.EffectiveRate,
                dyn.Applied);
        }).ToList();
    }

    private static ListingCategory? ResolveListingCategory(ParkingSearchDto criteria)
    {
        if (criteria.ListingCategory.HasValue)
            return criteria.ListingCategory;
        if (criteria.IsResidential == true)
            return ListingCategory.Residential;
        if (criteria.IsResidential == false)
            return ListingCategory.Commercial;
        return null;
    }

    private static IQueryable<ParkingSpace> ApplySearchFilters(
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
        double? minRating,
        bool? hasEvCharging = null,
        ListingCategory? listingCategory = null)
    {
        if (!string.IsNullOrEmpty(state))
            query = query.Where(p => p.State.ToLower() == state.ToLower());

        if (!string.IsNullOrEmpty(city))
            query = query.Where(p => p.City.ToLower().Contains(city.ToLower()));

        if (!string.IsNullOrEmpty(address))
            query = query.Where(p => p.Address.ToLower().Contains(address.ToLower()) ||
                                     p.Title.ToLower().Contains(address.ToLower()));

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
                if (a.Equals("EV_Charging", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("EV Charging", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.HasEvCharging
                        || (p.Amenities != null && (p.Amenities.Contains("EV") || p.Amenities.Contains("EV_Charging"))));
                    continue;
                }

                query = query.Where(p => p.Amenities != null && p.Amenities.Contains(a));
            }
        }

        if (hasEvCharging == true)
            query = query.Where(p => p.HasEvCharging);

        if (listingCategory.HasValue)
            query = query.Where(p => p.ListingCategory == listingCategory.Value);

        if (minRating.HasValue)
            query = query.Where(p => p.AverageRating >= minRating.Value);

        return query;
    }

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
        public int ListingCategory { get; set; }
        public bool InstantBook { get; set; }
        public int TotalSpots { get; set; }
        public int AvailableSpots { get; set; }
        public bool IsDynamicPricingEnabled { get; set; }
        public decimal DynamicMinMultiplier { get; set; } = 0.80m;
        public decimal DynamicMaxMultiplier { get; set; } = 1.75m;
        public decimal PeakHourMultiplier { get; set; } = 1.25m;
        public decimal WeekendMultiplier { get; set; } = 1.15m;
        public string? TimeZoneId { get; set; }
    }
}

