using ParkingApp.Application.Caching;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.Queries.Parking;

/// <summary>
/// One semantic discovery request for cache keys and list, count, and map queries.
/// </summary>
internal static class DiscoverySearchCanonical
{
    /// <summary>
    /// Snaps latitude, longitude, and radius to the same values the cache key encodes.
    /// Other fields, including <see cref="ParkingSearchDto.StartDateTime"/>, are unchanged.
    /// </summary>
    public static ParkingSearchDto Apply(ParkingSearchDto dto)
    {
        var latitude = CacheKeys.CanonicalCoordinate(dto.Latitude);
        var longitude = CacheKeys.CanonicalCoordinate(dto.Longitude);
        var radiusKm = CacheKeys.CanonicalRadius(dto.RadiusKm);
        if (Nullable.Equals(latitude, dto.Latitude)
            && Nullable.Equals(longitude, dto.Longitude)
            && Nullable.Equals(radiusKm, dto.RadiusKm))
        {
            return dto;
        }

        return dto with
        {
            Latitude = latitude,
            Longitude = longitude,
            RadiusKm = radiusKm
        };
    }

    /// <summary>
    /// Search and map apply the EV predicate only when the flag is true.
    /// Null and false are the same query.
    /// </summary>
    public static bool RequireEvCharging(ParkingSearchDto dto) => dto.HasEvCharging == true;

    /// <summary>
    /// ListingCategory wins. Otherwise IsResidential true/false selects Residential/Commercial.
    /// </summary>
    public static ListingCategory? ResolveListingCategory(ParkingSearchDto dto)
    {
        if (dto.ListingCategory.HasValue)
            return dto.ListingCategory;
        if (dto.IsResidential == true)
            return ListingCategory.Residential;
        if (dto.IsResidential == false)
            return ListingCategory.Commercial;
        return null;
    }

    public static string? ListingCategoryKey(ParkingSearchDto dto) =>
        ResolveListingCategory(dto)?.ToString();

    public static string AmenitiesKey(IReadOnlyList<string>? amenities)
    {
        if (amenities == null || amenities.Count == 0)
            return "";

        return string.Join(",", amenities.OrderBy(static a => a, StringComparer.Ordinal));
    }
}
