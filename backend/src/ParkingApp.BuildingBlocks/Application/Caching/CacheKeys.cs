using System.Globalization;

namespace ParkingApp.Application.Caching;

/// <summary>
/// Canonical cache key formats. All readers and invalidators must use these helpers
/// so invalidation cannot miss keys due to string typos or drift.
/// </summary>
public static class CacheKeys
{
    // ── Exact keys ──────────────────────────────────────────────────────────

    public static string Parking(Guid parkingSpaceId) => $"parking:{parkingSpaceId:D}";

    public static string Reviews(Guid parkingSpaceId) => $"reviews:parking:{parkingSpaceId:D}";

    public static string User(Guid userId) => $"user:{userId:D}";

    public static string VendorDashboard(Guid vendorId) => $"dashboard:vendor:{vendorId:D}";

    public static string MemberDashboard(Guid memberId) => $"dashboard:member:{memberId:D}";

    /// <summary>Header/nav total unread chat messages for a user (short TTL; invalidate on send/read).</summary>
    public static string ChatUnread(Guid userId) => $"chat:unread:{userId:D}";

    /// <summary>Vendor badge: pending booking approvals.</summary>
    public static string PendingRequestsCount(Guid vendorId) => $"dashboard:pending-count:{vendorId:D}";

    /// <summary>Owner parking list (includes embedded active reservations).</summary>
    public static string OwnerParkings(Guid ownerId) => $"owner-parkings:{ownerId:D}";

    public static string CompanyQuota(Guid companyId) => $"company-quota:{companyId:D}";

    public static string CompanyDashboard(Guid companyId) => $"company-dashboard:{companyId:D}";

    /// <summary>User's active parking passes (pricing-critical).</summary>
    public static string UserActivePasses(Guid userId) => $"user-passes:{userId:D}";

    /// <param name="mlEnabled">Included so toggling Forecast:EnableMl does not serve stale hybrid/deterministic results.</param>
    public static string ParkingForecast(Guid parkingSpaceId, int horizonHours, int intervalMinutes, bool mlEnabled = false) =>
        $"parking-forecast:{parkingSpaceId:D}:{horizonHours}:{intervalMinutes}:ml:{(mlEnabled ? 1 : 0)}";

    public static string OwnerForecast(Guid ownerId, int horizonHours, int intervalMinutes, bool mlEnabled = false) =>
        $"owner-parking-forecast:{ownerId:D}:{horizonHours}:{intervalMinutes}:ml:{(mlEnabled ? 1 : 0)}";

    /// <summary>
    /// Search cache key. Includes rounded geo so different locations do not share entries.
    /// Callers must pass already-clamped page/pageSize.
    /// </summary>
    /// <param name="useOsrmOnSearch">
    /// Included so toggling Routing:UseOsrmOnSearch does not serve OSRM distances under a haversine cache entry (or vice versa).
    /// Default true matches historical search behavior.
    /// </param>
    public static string Search(
        string? state,
        string? city,
        string? address,
        object? parkingType,
        object? vehicleType,
        decimal? minPrice,
        decimal? maxPrice,
        string amenitiesKey,
        int page,
        int pageSize,
        double? latitude = null,
        double? longitude = null,
        double? radiusKm = null,
        double? minRating = null,
        string? sortBy = null,
        bool sortDescending = false,
        bool useOsrmOnSearch = true) =>
        string.Create(CultureInfo.InvariantCulture,
            $"search:{state}:{city}:{address}:{parkingType}:{vehicleType}:{minPrice}:{maxPrice}:{amenitiesKey}:{page}:{pageSize}:geo:{RoundCoord(latitude)}:{RoundCoord(longitude)}:{RoundRadius(radiusKm)}:r:{minRating}:s:{sortBy}:{sortDescending}:osrm:{(useOsrmOnSearch ? 1 : 0)}");

    /// <summary>
    /// Map pins cache key. Coordinates are rounded for stable keys under minor GPS jitter.
    /// </summary>
    public static string Map(
        string? state,
        string? city,
        string? address,
        object? parkingType,
        object? vehicleType,
        decimal? minPrice,
        decimal? maxPrice,
        double? radiusKm,
        double? latitude,
        double? longitude,
        string amenitiesKey) =>
        string.Create(CultureInfo.InvariantCulture,
            $"map:{state}:{city}:{address}:{parkingType}:{vehicleType}:{minPrice}:{maxPrice}:{RoundRadius(radiusKm)}:{RoundCoord(latitude)}:{RoundCoord(longitude)}:{amenitiesKey}");

    /// <summary>~11 m precision at equator; enough to separate nearby searches without exploding key cardinality.</summary>
    public static string RoundCoord(double? value) =>
        value.HasValue
            ? value.Value.ToString("0.0000", CultureInfo.InvariantCulture)
            : string.Empty;

    public static string RoundRadius(double? radiusKm) =>
        radiusKm.HasValue
            ? Math.Round(radiusKm.Value, 1).ToString("0.0", CultureInfo.InvariantCulture)
            : string.Empty;

    // ── Pattern invalidation (namespace:* → version bump on Redis) ──────────

    public const string SearchAll = "search:*";
    public const string MapAll = "map:*";
    public const string ParkingForecastAll = "parking-forecast:*";
    public const string OwnerForecastAll = "owner-parking-forecast:*";
}
