using System.ComponentModel.DataAnnotations;

namespace ParkingApp.Marketplace.Contracts.DTOs;

public record ParkingAncillaryServiceDto(
    Guid Id,
    Guid ParkingSpaceId,
    string Name,
    string? Description,
    decimal Price,
    int? DurationMinutes,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt
);

public record CreateParkingAncillaryServiceDto(
    [Required] Guid ParkingSpaceId,
    [Required, MaxLength(120)] string Name,
    [Range(0, 1_000_000)] decimal Price,
    [MaxLength(500)] string? Description = null,
    [Range(0, 24 * 60)] int? DurationMinutes = null,
    int SortOrder = 0,
    bool IsActive = true
);

public record UpdateParkingAncillaryServiceDto(
    [MaxLength(120)] string? Name = null,
    [MaxLength(500)] string? Description = null,
    [Range(0, 1_000_000)] decimal? Price = null,
    [Range(0, 24 * 60)] int? DurationMinutes = null,
    bool? IsActive = null,
    int? SortOrder = null
);

public record BookingAncillaryLineDto(
    Guid Id,
    Guid? ServiceId,
    string SnapshotName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);
