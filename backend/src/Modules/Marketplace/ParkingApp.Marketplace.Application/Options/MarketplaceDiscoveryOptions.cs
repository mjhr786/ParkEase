namespace ParkingApp.Marketplace.Application.Options;

/// <summary>
/// Free-tier friendly caps for public discovery (search list + map pins).
/// Bound from configuration section <c>Marketplace</c>.
/// </summary>
public sealed class MarketplaceDiscoveryOptions
{
    public const string SectionName = "Marketplace";

    public SearchOptions Search { get; set; } = new();
    public MapOptions Map { get; set; } = new();

    public sealed class SearchOptions
    {
        /// <summary>Maximum page size clients may request (clamped server-side).</summary>
        public int MaxPageSize { get; set; } = 40;

        /// <summary>Cache TTL minutes for search results (invalidated via versioned namespace).</summary>
        public int CacheMinutes { get; set; } = 5;
    }

    public sealed class MapOptions
    {
        /// <summary>Maximum pins returned for a map query.</summary>
        public int MaxPins { get; set; } = 500;

        /// <summary>Cache TTL minutes for map pins.</summary>
        public int CacheMinutes { get; set; } = 5;
    }
}
