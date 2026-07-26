using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Marketplace write-side contract for company-owned parking lots.
/// Corporate handlers call this instead of Marketplace repositories/domain.
/// Implementations stage changes without SaveChanges; callers commit via their UoW.
/// </summary>
public interface ICompanyOwnedParkingSpaceService
{
    Task<CompanyOwnedParkingSpaceOpResult> CreateAsync(
        Guid companyId,
        Guid adminUserId,
        CreateParkingSpaceDto dto,
        CancellationToken cancellationToken = default);

    Task<CompanyOwnedParkingSpaceOpResult> UpdateAsync(
        Guid companyId,
        Guid parkingSpaceId,
        CompanyOwnedParkingSpaceUpdate update,
        CancellationToken cancellationToken = default);

    Task<CompanyOwnedParkingSpaceOpResult> ToggleActiveAsync(
        Guid companyId,
        Guid parkingSpaceId,
        CancellationToken cancellationToken = default);

    Task<CompanyOwnedParkingSpaceOpResult> RetireAsync(
        Guid companyId,
        Guid parkingSpaceId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);
}

public sealed record CompanyOwnedParkingSpaceOpResult(
    bool Success,
    string Message,
    CompanyOwnedParkingSpaceDetail? Space = null);

public sealed record CompanyOwnedParkingSpaceDetail(
    Guid Id,
    Guid CompanyId,
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
    IReadOnlyList<string> Amenities,
    IReadOnlyList<VehicleType> AllowedVehicleTypes,
    IReadOnlyList<string> ImageUrls,
    bool IsActive,
    bool IsVerified,
    string? SpecialInstructions,
    string? ZoneCode,
    DateTime CreatedAt,
    Guid OwnerId);

public sealed record CompanyOwnedParkingSpaceUpdate(
    string? Title = null,
    string? Description = null,
    string? Address = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? PostalCode = null,
    double? Latitude = null,
    double? Longitude = null,
    ParkingType? ParkingType = null,
    int? TotalSpots = null,
    decimal? HourlyRate = null,
    decimal? DailyRate = null,
    decimal? WeeklyRate = null,
    decimal? MonthlyRate = null,
    TimeSpan? OpenTime = null,
    TimeSpan? CloseTime = null,
    bool? Is24Hours = null,
    IReadOnlyList<string>? Amenities = null,
    IReadOnlyList<VehicleType>? AllowedVehicleTypes = null,
    IReadOnlyList<string>? ImageUrls = null,
    string? SpecialInstructions = null,
    string? ZoneCode = null);
