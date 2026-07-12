using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

public class UpdateCorporateParkingSpaceHandler : ICommandHandler<UpdateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;

    public UpdateCorporateParkingSpaceHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace)
    {
        _corporate = corporate;
        _marketplace = marketplace;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(UpdateCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can edit company-owned parking.", null);
        }

        var parking = await _marketplace.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company-owned parking space not found.", null);
        }

        var dto = command.Dto;
        try
        {
            parking.UpdateDetails(
                title: dto.Title,
                description: dto.Description,
                address: dto.Address,
                city: dto.City,
                state: dto.State,
                country: dto.Country,
                postalCode: dto.PostalCode,
                zoneCode: dto.ZoneCode,
                latitude: dto.Latitude,
                longitude: dto.Longitude,
                parkingType: dto.ParkingType,
                totalSpots: dto.TotalSpots,
                hourlyRate: dto.HourlyRate,
                dailyRate: dto.DailyRate,
                weeklyRate: dto.WeeklyRate,
                monthlyRate: dto.MonthlyRate,
                openTime: dto.OpenTime,
                closeTime: dto.CloseTime,
                is24Hours: dto.Is24Hours,
                amenities: dto.Amenities,
                allowedVehicleTypes: dto.AllowedVehicleTypes?.Select(v => v.ToString()),
                imageUrls: dto.ImageUrls,
                specialInstructions: dto.SpecialInstructions);

            _marketplace.ParkingSpaces.Update(parking);
            await _corporate.SaveChangesAsync(ct);

            return new ApiResponse<CorporateParkingSpaceDto>(
                true,
                "Company-owned parking space updated.",
                CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
        }
        catch (DomainException ex)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, ex.Message, null);
        }
    }
}
