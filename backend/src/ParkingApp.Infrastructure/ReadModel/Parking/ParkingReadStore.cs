using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.ReadModel.Parking;

public sealed class ParkingReadStore : IParkingReadStore
{
    private readonly ApplicationDbContext _db;
    private readonly ISqlConnectionFactory _sql;

    public ParkingReadStore(ApplicationDbContext db, ISqlConnectionFactory sql)
    {
        _db = db;
        _sql = sql;
    }

    public async Task<IReadOnlyList<ParkingSpace>> SearchAsync(ParkingSearchDto criteria, CancellationToken ct = default)
    {
        // Parity with SearchParkingHandler → IParkingSpaceRepository.SearchAsync call site
        // (parkingType / vehicleType historically not passed from the handler).
        var amenities = criteria.Amenities != null ? string.Join(",", criteria.Amenities) : null;

        var query = _db.ParkingSpaces
            .AsNoTracking()
            .Include(p => p.Owner)
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
            criteria.MinRating);

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
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);

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
            criteria.MinRating);

        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<ParkingMapDto>> GetMapPinsAsync(ParkingSearchDto criteria, CancellationToken ct = default)
    {
        var sql = new StringBuilder();
        var parameters = new DynamicParameters();

        // Parity with marketplace search: exclude company-only inventory from public map.
        sql.Append("""
            SELECT "Id", "Title", "Address", "City", "Latitude", "Longitude",
                   "HourlyRate", "ImageUrls" AS "ThumbnailUrl", "AverageRating", "ParkingType"
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
                var paramName = $"Amenity{i}";
                sql.Append($""" AND "Amenities" LIKE '%' || @{paramName} || '%'""");
                parameters.Add(paramName, criteria.Amenities[i]);
            }
        }

        sql.Append(" LIMIT 2000");

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<MapRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct));

        return rows.Select(r => new ParkingMapDto(
            r.Id,
            r.Title,
            r.Address,
            r.City,
            r.Latitude,
            r.Longitude,
            r.HourlyRate,
            ExtractThumbnail(r.ThumbnailUrl),
            r.AverageRating,
            (ParkingType)r.ParkingType)).ToList();
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
        double? minRating)
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
                query = query.Where(p => p.Amenities != null && p.Amenities.Contains(a));
            }
        }

        if (minRating.HasValue)
            query = query.Where(p => p.AverageRating >= minRating.Value);

        return query;
    }

    private static string? ExtractThumbnail(string? imageUrls)
    {
        if (string.IsNullOrEmpty(imageUrls)) return null;
        var commaIndex = imageUrls.IndexOf(',');
        return commaIndex > -1 ? imageUrls[..commaIndex] : imageUrls;
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
    }
}
