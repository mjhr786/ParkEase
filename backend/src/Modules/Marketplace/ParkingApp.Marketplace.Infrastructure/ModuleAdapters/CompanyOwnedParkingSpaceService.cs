using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingSpaceOwnershipType = ParkingApp.Marketplace.Contracts.Enums.ParkingSpaceOwnershipType;

namespace ParkingApp.Marketplace.Infrastructure.ModuleAdapters;

/// <summary>
/// Marketplace adapter for company-owned parking CRUD. Does not call SaveChanges.
/// </summary>
internal sealed class CompanyOwnedParkingSpaceService : ICompanyOwnedParkingSpaceService
{
    private readonly IMarketplaceUnitOfWork _marketplace;

    public CompanyOwnedParkingSpaceService(IMarketplaceUnitOfWork marketplace) => _marketplace = marketplace;

    public async Task<CompanyOwnedParkingSpaceOpResult> CreateAsync(
        Guid companyId,
        Guid adminUserId,
        CreateParkingSpaceDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = dto.ToCompanyEntity(adminUserId, companyId);
            await _marketplace.ParkingSpaces.AddAsync(parking, cancellationToken);
            return new CompanyOwnedParkingSpaceOpResult(
                true,
                "Company-owned parking space created.",
                Map(parking, companyId));
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new CompanyOwnedParkingSpaceOpResult(false, ex.Message);
        }
    }

    public async Task<CompanyOwnedParkingSpaceOpResult> UpdateAsync(
        Guid companyId,
        Guid parkingSpaceId,
        CompanyOwnedParkingSpaceUpdate update,
        CancellationToken cancellationToken = default)
    {
        var parking = await GetCompanyOwnedAsync(companyId, parkingSpaceId, cancellationToken);
        if (parking is null)
            return new CompanyOwnedParkingSpaceOpResult(false, "Company-owned parking space not found.");

        try
        {
            parking.UpdateDetails(
                title: update.Title,
                description: update.Description,
                address: update.Address,
                city: update.City,
                state: update.State,
                country: update.Country,
                postalCode: update.PostalCode,
                zoneCode: update.ZoneCode,
                latitude: update.Latitude,
                longitude: update.Longitude,
                parkingType: update.ParkingType,
                totalSpots: update.TotalSpots,
                hourlyRate: update.HourlyRate,
                dailyRate: update.DailyRate,
                weeklyRate: update.WeeklyRate,
                monthlyRate: update.MonthlyRate,
                openTime: update.OpenTime,
                closeTime: update.CloseTime,
                is24Hours: update.Is24Hours,
                amenities: update.Amenities,
                allowedVehicleTypes: update.AllowedVehicleTypes?.Select(v => v.ToString()),
                imageUrls: update.ImageUrls,
                specialInstructions: update.SpecialInstructions);

            _marketplace.ParkingSpaces.Update(parking);
            return new CompanyOwnedParkingSpaceOpResult(
                true,
                "Company-owned parking space updated.",
                Map(parking, companyId));
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new CompanyOwnedParkingSpaceOpResult(false, ex.Message);
        }
    }

    public async Task<CompanyOwnedParkingSpaceOpResult> ToggleActiveAsync(
        Guid companyId,
        Guid parkingSpaceId,
        CancellationToken cancellationToken = default)
    {
        var parking = await GetCompanyOwnedAsync(companyId, parkingSpaceId, cancellationToken);
        if (parking is null)
            return new CompanyOwnedParkingSpaceOpResult(false, "Company-owned parking space not found.");

        parking.ToggleActive();
        _marketplace.ParkingSpaces.Update(parking);

        return new CompanyOwnedParkingSpaceOpResult(
            true,
            parking.IsActive ? "Parking space activated." : "Parking space deactivated.",
            Map(parking, companyId));
    }

    public async Task<CompanyOwnedParkingSpaceOpResult> RetireAsync(
        Guid companyId,
        Guid parkingSpaceId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var parking = await GetCompanyOwnedAsync(companyId, parkingSpaceId, cancellationToken);
        if (parking is null)
            return new CompanyOwnedParkingSpaceOpResult(false, "Company-owned parking space not found.");

        var hasActiveBookings = await _marketplace.Bookings.HasBlockingBookingsForSpaceAsync(
            parkingSpaceId, DateTime.UtcNow, cancellationToken);
        if (hasActiveBookings)
            return new CompanyOwnedParkingSpaceOpResult(false, "Cannot retire parking space with active bookings.");

        parking.Retire(adminUserId);
        _marketplace.ParkingSpaces.Update(parking);

        return new CompanyOwnedParkingSpaceOpResult(
            true,
            "Company-owned parking space retired.",
            Map(parking, companyId));
    }

    private async Task<ParkingSpace?> GetCompanyOwnedAsync(
        Guid companyId,
        Guid parkingSpaceId,
        CancellationToken cancellationToken)
    {
        var parking = await _marketplace.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking is null
            || parking.IsDeleted
            || parking.CompanyOwnerId != companyId
            || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return null;
        }

        return parking;
    }

    private static CompanyOwnedParkingSpaceDetail Map(ParkingSpace parking, Guid companyId)
    {
        return new CompanyOwnedParkingSpaceDetail(
            parking.Id,
            companyId,
            parking.Title,
            parking.Description,
            parking.Address,
            parking.City,
            parking.State,
            parking.Country,
            parking.PostalCode,
            parking.Latitude,
            parking.Longitude,
            parking.ParkingType,
            parking.TotalSpots,
            parking.AvailableSpots,
            parking.HourlyRate,
            parking.DailyRate,
            parking.WeeklyRate,
            parking.MonthlyRate,
            parking.OpenTime,
            parking.CloseTime,
            parking.Is24Hours,
            SplitCsv(parking.Amenities),
            ParseVehicleTypes(parking.AllowedVehicleTypes),
            SplitCsv(parking.ImageUrls),
            parking.IsActive,
            parking.IsVerified,
            parking.SpecialInstructions,
            parking.ZoneCode,
            parking.CreatedAt,
            parking.OwnerId);
    }

    private static IReadOnlyList<string> SplitCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<VehicleType> ParseVehicleTypes(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<VehicleType>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<VehicleType>(s, ignoreCase: true, out var v) ? v : (VehicleType?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }
}
