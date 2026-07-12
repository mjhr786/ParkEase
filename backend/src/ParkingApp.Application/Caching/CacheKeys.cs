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

    /// <summary>Vendor badge: pending booking approvals.</summary>
    public static string PendingRequestsCount(Guid vendorId) => $"dashboard:pending-count:{vendorId:D}";

    /// <summary>Owner parking list (includes embedded active reservations).</summary>
    public static string OwnerParkings(Guid ownerId) => $"owner-parkings:{ownerId:D}";

    public static string CompanyQuota(Guid companyId) => $"company-quota:{companyId:D}";

    public static string CompanyDashboard(Guid companyId) => $"company-dashboard:{companyId:D}";

    /// <summary>User's active parking passes (pricing-critical).</summary>
    public static string UserActivePasses(Guid userId) => $"user-passes:{userId:D}";

    public static string ParkingForecast(Guid parkingSpaceId, int horizonHours, int intervalMinutes) =>
        $"parking-forecast:{parkingSpaceId:D}:{horizonHours}:{intervalMinutes}";

    public static string OwnerForecast(Guid ownerId, int horizonHours, int intervalMinutes) =>
        $"owner-parking-forecast:{ownerId:D}:{horizonHours}:{intervalMinutes}";

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
        int pageSize) =>
        $"search:{state}:{city}:{address}:{parkingType}:{vehicleType}:{minPrice}:{maxPrice}:{amenitiesKey}:{page}:{pageSize}";

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
        $"map:{state}:{city}:{address}:{parkingType}:{vehicleType}:{minPrice}:{maxPrice}:{radiusKm}:{latitude}:{longitude}:{amenitiesKey}";

    // ── Pattern invalidation (namespace:* → version bump on Redis) ──────────

    public const string SearchAll = "search:*";
    public const string MapAll = "map:*";
    public const string ParkingForecastAll = "parking-forecast:*";
    public const string OwnerForecastAll = "owner-parking-forecast:*";
}
