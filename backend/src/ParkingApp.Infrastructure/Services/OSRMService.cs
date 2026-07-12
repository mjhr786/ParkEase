using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class OSRMService : IRoutingService
{
    /// <summary>Public demo server typically allows up to ~100 coordinates per request.</summary>
    private const int MaxCoordinatesPerRequest = 80;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OSRMService> _logger;

    public OSRMService(HttpClient httpClient, ILogger<OSRMService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri("https://router.project-osrm.org/");
    }

    public async Task<List<(double Distance, int Duration)>> GetBatchRoutingAsync(
        double startLat,
        double startLng,
        List<(double Lat, double Lng)> destinations,
        CancellationToken cancellationToken = default)
    {
        if (destinations == null || destinations.Count == 0)
        {
            return new List<(double, int)>();
        }

        // Always seed with haversine so search never depends solely on OSRM availability.
        var results = destinations
            .Select(d => HaversineKmAndMinutes(startLat, startLng, d.Lat, d.Lng))
            .ToList();

        if (!IsValidCoordinate(startLat, startLng))
        {
            _logger.LogWarning(
                "Skipping OSRM routing: invalid start coordinates ({Lat}, {Lng})",
                startLat,
                startLng);
            return results;
        }

        var valid = destinations
            .Select((d, index) => (Dest: d, Index: index))
            .Where(x => IsValidCoordinate(x.Dest.Lat, x.Dest.Lng))
            .ToList();

        if (valid.Count == 0)
        {
            _logger.LogWarning("Skipping OSRM routing: no valid destination coordinates");
            return results;
        }

        // Chunk so public OSRM limits and URL length do not blow up the request.
        foreach (var chunk in valid.Chunk(MaxCoordinatesPerRequest - 1))
        {
            var chunkList = chunk.ToList();
            try
            {
                var osrmResults = await QueryOsrmTableAsync(
                    startLat,
                    startLng,
                    chunkList.Select(c => c.Dest).ToList(),
                    cancellationToken);

                for (var i = 0; i < chunkList.Count; i++)
                {
                    if (i < osrmResults.Count && osrmResults[i].Distance > 0)
                    {
                        results[chunkList[i].Index] = osrmResults[i];
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OSRM API for batch routing (chunk size {Count})", chunkList.Count);
                // Keep haversine values for this chunk.
            }
        }

        return results;
    }

    private async Task<List<(double Distance, int Duration)>> QueryOsrmTableAsync(
        double startLat,
        double startLng,
        List<(double Lat, double Lng)> destinations,
        CancellationToken cancellationToken)
    {
        var inv = CultureInfo.InvariantCulture;
        var coordParts = new List<string>(destinations.Count + 1)
        {
            $"{startLng.ToString(inv)},{startLat.ToString(inv)}"
        };
        coordParts.AddRange(destinations.Select(d => $"{d.Lng.ToString(inv)},{d.Lat.ToString(inv)}"));

        // Explicit destinations indexes (1..N) avoid source-to-source noise and match response columns 1:1.
        var destinationIndexes = string.Join(";", Enumerable.Range(1, destinations.Count));
        var url =
            $"table/v1/driving/{string.Join(";", coordParts)}" +
            $"?sources=0&destinations={destinationIndexes}&annotations=distance,duration";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OSRM Table API returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                Truncate(body, 400));
            return destinations.Select(d => HaversineKmAndMinutes(startLat, startLng, d.Lat, d.Lng)).ToList();
        }

        var table = JsonSerializer.Deserialize<OSRMTableResponse>(body, JsonOptions);
        if (table?.Distances == null || table.Durations == null)
        {
            _logger.LogWarning("OSRM Table API returned empty results");
            return destinations.Select(d => HaversineKmAndMinutes(startLat, startLng, d.Lat, d.Lng)).ToList();
        }

        var results = new List<(double Distance, int Duration)>(destinations.Count);
        for (var i = 0; i < destinations.Count; i++)
        {
            var distMeters = GetMatrixValue(table.Distances, 0, i);
            var durationSeconds = GetMatrixValue(table.Durations, 0, i);

            if (distMeters is null || durationSeconds is null || distMeters < 0 || durationSeconds < 0)
            {
                results.Add(HaversineKmAndMinutes(startLat, startLng, destinations[i].Lat, destinations[i].Lng));
                continue;
            }

            results.Add((
                Math.Round(distMeters.Value / 1000.0, 1),
                (int)Math.Ceiling(durationSeconds.Value / 60.0)));
        }

        return results;
    }

    private static double? GetMatrixValue(double?[][] matrix, int row, int col)
    {
        if (row < 0 || row >= matrix.Length || matrix[row] == null)
        {
            return null;
        }

        if (col < 0 || col >= matrix[row].Length)
        {
            return null;
        }

        return matrix[row][col];
    }

    /// <summary>
    /// Rejects NaN/Inf, out-of-range values, and (0,0) which is a common unset placeholder
    /// and causes the public OSRM demo server to respond with HTTP 400.
    /// </summary>
    internal static bool IsValidCoordinate(double lat, double lng)
    {
        if (double.IsNaN(lat) || double.IsNaN(lng) || double.IsInfinity(lat) || double.IsInfinity(lng))
        {
            return false;
        }

        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            return false;
        }

        // (0,0) is in the ocean and almost never a real parking pin; OSRM demo rejects it with 400.
        if (Math.Abs(lat) < 1e-9 && Math.Abs(lng) < 1e-9)
        {
            return false;
        }

        return true;
    }

    internal static (double DistanceKm, int DurationMinutes) HaversineKmAndMinutes(
        double startLat,
        double startLng,
        double endLat,
        double endLng)
    {
        if (!IsValidCoordinate(startLat, startLng) || !IsValidCoordinate(endLat, endLng))
        {
            return (0.0, 0);
        }

        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(endLat - startLat);
        var dLon = DegreesToRadians(endLng - startLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(startLat)) * Math.Cos(DegreesToRadians(endLat)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distanceKm = Math.Round(earthRadiusKm * c, 1);

        // Rough city-driving estimate (~30 km/h average including stops).
        var durationMinutes = distanceKm <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(distanceKm / 30.0 * 60.0));

        return (distanceKm, durationMinutes);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= max ? value : value[..max];
    }

    private class OSRMTableResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        // Cells may be null when a route cannot be found.
        [JsonPropertyName("distances")]
        public double?[][]? Distances { get; set; }

        [JsonPropertyName("durations")]
        public double?[][]? Durations { get; set; }
    }
}
