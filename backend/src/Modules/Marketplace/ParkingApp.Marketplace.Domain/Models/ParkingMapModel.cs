using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Domain.Models;

public record ParkingMapModel(
    Guid Id,
    string Title,
    string Address,
    string City,
    double Latitude,
    double Longitude,
    decimal HourlyRate,
    string? ImageUrls,
    double AverageRating,
    ParkingType ParkingType
);

