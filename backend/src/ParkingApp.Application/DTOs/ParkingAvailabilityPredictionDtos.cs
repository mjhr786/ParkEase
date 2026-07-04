namespace ParkingApp.Application.DTOs;

public record ParkingAvailabilityBucketDto(
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    int DeterministicBookedSpots,
    int PredictedBookedSpots,
    int PredictedAvailableSpots,
    double HistoricalOccupancyRate,
    double PredictedOccupancyRate,
    double ConfidenceScore,
    string AvailabilityBand,
    bool IsLiveWindow
);

public record ParkingAvailabilityForecastDto(
    Guid ParkingSpaceId,
    string ParkingTitle,
    bool IsListingActive,
    int TotalSpots,
    DateTime GeneratedAtUtc,
    int HorizonHours,
    int IntervalMinutes,
    int CurrentPredictedBookedSpots,
    int CurrentPredictedAvailableSpots,
    double CurrentPredictedOccupancyRate,
    double CurrentConfidenceScore,
    string CurrentAvailabilityBand,
    int AveragePredictedAvailableSpotsAcrossForecast,
    int PeakPredictedBookedSpotsAcrossForecast,
    DateTime? LikelyFullAtUtc,
    List<ParkingAvailabilityBucketDto> Buckets
);
