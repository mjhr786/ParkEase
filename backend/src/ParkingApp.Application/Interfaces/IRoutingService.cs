namespace ParkingApp.Application.Interfaces;

public interface IRoutingService
{
    /// <summary>
    /// Calculates distances and durations from a start point to multiple destinations in one batch.
    /// Returns a list of (Distance in KM, Duration in Minutes).
    /// </summary>
    Task<List<(double Distance, int Duration)>> GetBatchRoutingAsync(
        double startLat, 
        double startLng, 
        List<(double Lat, double Lng)> destinations, 
        CancellationToken cancellationToken = default);
}
