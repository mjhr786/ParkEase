namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IRoutingService
{
    /// <summary>
    /// Calculates road distances and durations from a start point to multiple destinations (OSRM),
    /// with haversine fallback when OSRM is unavailable or returns gaps.
    /// Returns a list of (Distance in KM, Duration in Minutes) aligned with <paramref name="destinations"/>.
    /// </summary>
    Task<List<(double Distance, int Duration)>> GetBatchRoutingAsync(
        double startLat,
        double startLng,
        List<(double Lat, double Lng)> destinations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Straight-line (haversine) distance and rough city-drive ETA. No external HTTP.
    /// Same return shape as <see cref="GetBatchRoutingAsync"/> for drop-in use on search.
    /// </summary>
    List<(double Distance, int Duration)> GetBatchHaversine(
        double startLat,
        double startLng,
        List<(double Lat, double Lng)> destinations);
}
