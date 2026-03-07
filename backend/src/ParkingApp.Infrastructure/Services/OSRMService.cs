using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class OSRMService : IRoutingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OSRMService> _logger;

    public OSRMService(HttpClient httpClient, ILogger<OSRMService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://router.project-osrm.org/");
    }

    public async Task<List<(double Distance, int Duration)>> GetBatchRoutingAsync(
        double startLat, 
        double startLng, 
        List<(double Lat, double Lng)> destinations, 
        CancellationToken cancellationToken = default)
    {
        if (destinations == null || !destinations.Any())
            return new List<(double, int)>();

        try
        {
            // OSRM Table API: table/v1/driving/source_lng,source_lat;dest1_lng,dest1_lat;...
            // sources=0 (the first coordinate)
            var coords = string.Join(";", destinations.Select(d => $"{d.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{d.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            var url = $"table/v1/driving/{startLng.ToString(System.Globalization.CultureInfo.InvariantCulture)},{startLat.ToString(System.Globalization.CultureInfo.InvariantCulture)};{coords}?sources=0&annotations=distance,duration";

            var response = await _httpClient.GetFromJsonAsync<OSRMTableResponse>(url, cancellationToken);

            if (response?.Distances == null || response.Durations == null)
            {
                _logger.LogWarning("OSRM Table API returned empty results");
                return destinations.Select(_ => (0.0, 0)).ToList();
            }

            var results = new List<(double Distance, int Duration)>();
            // The result arrays are [sources][destinations]
            // We have 1 source (index 0) and N destinations
            for (int i = 0; i < destinations.Count; i++)
            {
                // destinations indices in response start from 1 (index 0 is the source itself)
                var distMeters = response.Distances[0][i + 1];
                var durationSeconds = response.Durations[0][i + 1];

                results.Add((
                    Math.Round(distMeters / 1000.0, 1), // Meters to KM
                    (int)Math.Ceiling(durationSeconds / 60.0) // Seconds to Minutes
                ));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OSRM API for batch routing");
            // Fallback to 0s instead of throwing to avoid breaking the search page
            return destinations.Select(_ => (0.0, 0)).ToList();
        }
    }

    private class OSRMTableResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("distances")]
        public double[][]? Distances { get; set; }

        [JsonPropertyName("durations")]
        public double[][]? Durations { get; set; }
    }
}
