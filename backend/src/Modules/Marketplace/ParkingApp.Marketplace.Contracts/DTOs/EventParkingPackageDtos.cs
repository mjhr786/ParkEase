using System.ComponentModel.DataAnnotations;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Contracts.DTOs;

public record EventParkingPackageDto(
    Guid Id,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    string ParkingSpaceAddress,
    string? ParkingSpaceCity,
    string Title,
    string? Description,
    string? EventName,
    string? VenueName,
    DateTime EventStartUtc,
    DateTime EventEndUtc,
    DateTime SalesStartUtc,
    DateTime? SalesEndUtc,
    decimal PackagePrice,
    int TotalSpots,
    int SoldCount,
    int AvailableSpots,
    bool IsActive,
    bool IsOnSale,
    DateTime CreatedAt,
    Guid VenueEventId = default,
    string? ZoneName = null,
    int EarlyEntryMinutes = 0,
    int LateExitMinutes = 0,
    DateTime? AccessStartUtc = null,
    DateTime? AccessEndUtc = null
);

/// <summary>Public browse: one venue event with one or more lot/zone packages.</summary>
public record EventVenueOnSaleDto(
    Guid VenueEventId,
    string? EventName,
    string? VenueName,
    DateTime EventStartUtc,
    DateTime EventEndUtc,
    int ZoneCount,
    int TotalAvailableSpots,
    decimal MinPackagePrice,
    decimal MaxPackagePrice,
    IReadOnlyList<EventParkingPackageDto> Zones
);

public record EventPackageAnalyticsDto(
    Guid PackageId,
    Guid VenueEventId,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    string Title,
    string? EventName,
    string? VenueName,
    string? ZoneName,
    DateTime EventStartUtc,
    DateTime EventEndUtc,
    DateTime AccessStartUtc,
    DateTime AccessEndUtc,
    decimal PackagePrice,
    int TotalSpots,
    int SoldCount,
    int AvailableSpots,
    decimal SellThroughPercent,
    int ConfirmedBookingCount,
    decimal GrossRevenue,
    bool IsActive,
    bool IsOnSale
);

public record EventVenueAnalyticsDto(
    Guid VenueEventId,
    string? EventName,
    string? VenueName,
    DateTime EventStartUtc,
    DateTime EventEndUtc,
    int ZoneCount,
    int TotalSpots,
    int SoldCount,
    int AvailableSpots,
    decimal SellThroughPercent,
    decimal GrossRevenue,
    IReadOnlyList<EventPackageAnalyticsDto> Packages
);

public record CreateEventParkingPackageDto(
    [Required] Guid ParkingSpaceId,
    [Required] string Title,
    [Required] DateTime EventStartUtc,
    [Required] DateTime EventEndUtc,
    [Range(0, 1_000_000)] decimal PackagePrice,
    [Range(1, 10000)] int TotalSpots,
    string? Description = null,
    string? EventName = null,
    string? VenueName = null,
    DateTime? SalesStartUtc = null,
    DateTime? SalesEndUtc = null,
    /// <summary>Reuse an existing venue event id to add another lot/zone to the same event.</summary>
    Guid? VenueEventId = null,
    string? ZoneName = null,
    [Range(0, 1440)] int EarlyEntryMinutes = 0,
    [Range(0, 1440)] int LateExitMinutes = 0
);

public record UpdateEventParkingPackageDto(
    string? Title = null,
    string? Description = null,
    string? EventName = null,
    string? VenueName = null,
    DateTime? EventStartUtc = null,
    DateTime? EventEndUtc = null,
    decimal? PackagePrice = null,
    int? TotalSpots = null,
    DateTime? SalesStartUtc = null,
    DateTime? SalesEndUtc = null,
    bool? IsActive = null,
    Guid? VenueEventId = null,
    string? ZoneName = null,
    [Range(0, 1440)] int? EarlyEntryMinutes = null,
    [Range(0, 1440)] int? LateExitMinutes = null
);

public record PurchaseEventParkingPackageDto(
    [Required] VehicleType VehicleType,
    string? VehicleNumber = null,
    string? VehicleModel = null,
    string? VehicleColor = null
);
