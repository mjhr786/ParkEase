namespace ParkingApp.Marketplace.Application.Options;

/// <summary>
/// Routing for marketplace search (distance / ETA). Bound from configuration section <c>Routing</c>.
/// Default preserves existing behavior: OSRM is used when coordinates are present.
/// Set <see cref="UseOsrmOnSearch"/> to false on free tier to avoid outbound OSRM HTTP.
/// </summary>
public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>
    /// When true (default), search uses OSRM table API with haversine fallback (current production behavior).
    /// When false, search uses haversine-only distance/ETA (no external HTTP).
    /// </summary>
    public bool UseOsrmOnSearch { get; set; } = true;
}
