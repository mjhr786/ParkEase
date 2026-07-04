namespace ParkingApp.Application.DTOs;

public record ParkingAvailabilityModelInputDto(
    Guid ParkingSpaceId,
    int TotalSpots,
    decimal HourlyRate,
    int ParkingType,
    bool Is24Hours,
    bool IsListingActive,
    double AverageRating,
    int TotalReviews,
    DateTime BucketStartUtc,
    double WeeklyOccupancyRate,
    double DailyOccupancyRate,
    double Recent1OccupancyRate,
    double Recent3OccupancyRate,
    double Recent6OccupancyRate
);

public record ParkingAvailabilityModelPredictionDto(
    double PredictedOccupancyRate,
    double ModelConfidenceScore,
    int TrainingSampleCount,
    bool UsedMachineLearning
);
