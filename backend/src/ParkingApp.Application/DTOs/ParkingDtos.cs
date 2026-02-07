using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

public record ReservationPeriodDto(
    DateTime StartDateTime,
    DateTime EndDateTime
);

public record ParkingSpaceDto(
    Guid Id,
    Guid OwnerId,
    string OwnerName,
    string Title,
    string Description,
    string Address,
    string City,
    string State,
    string Country,
    string PostalCode,
    double Latitude,
    double Longitude,
    ParkingType ParkingType,
    int TotalSpots,
    int AvailableSpots,
    decimal HourlyRate,
    decimal DailyRate,
    decimal WeeklyRate,
    decimal MonthlyRate,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    bool Is24Hours,
    List<string> Amenities,
    List<VehicleType> AllowedVehicleTypes,
    List<string> ImageUrls,
    bool IsActive,
    bool IsVerified,
    double AverageRating,
    int TotalReviews,
    string? SpecialInstructions,
    DateTime CreatedAt,
    List<ReservationPeriodDto>? ActiveReservations = null
);

public record CreateParkingSpaceDto(
    [Required] string Title,
    [Required] string Description,
    [Required] string Address,
    [Required] string City,
    [Required] string State,
    [Required] string Country,
    [Required] string PostalCode,
    double Latitude,
    double Longitude,
    ParkingType ParkingType,
    [Range(1, 1000)] int TotalSpots,
    [Range(0, 10000)] decimal HourlyRate,
    [Range(0, 100000)] decimal DailyRate,
    [Range(0, 500000)] decimal WeeklyRate,
    [Range(0, 2000000)] decimal MonthlyRate,
    TimeSpan? OpenTime,
    TimeSpan? CloseTime,
    bool Is24Hours = true,
    List<string>? Amenities = null,
    List<VehicleType>? AllowedVehicleTypes = null,
    List<string>? ImageUrls = null,
    string? SpecialInstructions = null
);

public record UpdateParkingSpaceDto(
    string? Title,
    string? Description,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    ParkingType? ParkingType,
    int? TotalSpots,
    decimal? HourlyRate,
    decimal? DailyRate,
    decimal? WeeklyRate,
    decimal? MonthlyRate,
    TimeSpan? OpenTime,
    TimeSpan? CloseTime,
    bool? Is24Hours,
    List<string>? Amenities,
    List<VehicleType>? AllowedVehicleTypes,
    List<string>? ImageUrls,
    string? SpecialInstructions,
    bool? IsActive
);

public record ParkingSearchDto(
    string? City = null,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null,
    DateTime? StartDateTime = null,
    DateTime? EndDateTime = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    PricingType? PricingType = null,
    ParkingType? ParkingType = null,
    VehicleType? VehicleType = null,
    List<string>? Amenities = null,
    double? MinRating = null,
    string? SortBy = null, // price, rating, distance
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 20
);

public record ParkingSearchResultDto(
    List<ParkingSpaceDto> ParkingSpaces,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
