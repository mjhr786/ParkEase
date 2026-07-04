using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

public record ParkingPassDto(
    Guid Id,
    Guid UserId,
    string UserName,
    PassTypeKind PassType,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    PassCoverageType CoverageType,
    Guid? ParkingSpaceId,
    string? ParkingSpaceTitle,
    string? ParkingZoneCode,
    PassUsageMode UsageMode,
    int? DailyHourLimit,
    decimal DiscountPercentage,
    string State,
    bool IsActive,
    bool IsExpired,
    Guid? AllocatedByUserId,
    string? CorporateBatchReference,
    DateTime CreatedAt
);

public record ActiveParkingPassesDto(
    bool HasActivePass,
    List<ParkingPassDto> ActivePasses
);

public record CreateParkingPassDto(
    [Required] PassTypeKind PassType,
    [Required] DateTime StartDateUtc,
    [Required] DateTime EndDateUtc,
    Guid? ParkingSpaceId,
    string? ParkingZoneCode,
    [Required] PassUsageMode UsageMode,
    int? DailyHourLimit,
    [Range(0, 100)] decimal DiscountPercentage
);

public record AssignCorporatePassDto(
    [Required] IReadOnlyCollection<Guid> EmployeeUserIds,
    [Required] DateTime StartDateUtc,
    [Required] DateTime EndDateUtc,
    Guid? ParkingSpaceId,
    string? ParkingZoneCode,
    [Required] PassUsageMode UsageMode,
    int? DailyHourLimit,
    [Range(0, 100)] decimal DiscountPercentage,
    string? CorporateBatchReference = null
);

public record CorporatePassAssignmentResultDto(
    string CorporateBatchReference,
    int CreatedCount,
    List<ParkingPassDto> Passes
);
