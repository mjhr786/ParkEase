using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

internal sealed class UpdateCorporateParkingSpaceHandler
    : ICommandHandler<UpdateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly ICompanyOwnedParkingSpaceService _parkingSpaces;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public UpdateCorporateParkingSpaceHandler(
        ICorporateUnitOfWork corporate,
        ICompanyOwnedParkingSpaceService parkingSpaces,
        ICacheService cache,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _parkingSpaces = parkingSpaces;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(
        UpdateCorporateParkingSpaceCommand command,
        CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership is null || !membership.IsActive || !membership.IsAdmin)
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can edit company-owned parking.", null);

        var dto = command.Dto;
        var update = new CompanyOwnedParkingSpaceUpdate(
            Title: dto.Title,
            Description: dto.Description,
            Address: dto.Address,
            City: dto.City,
            State: dto.State,
            Country: dto.Country,
            PostalCode: dto.PostalCode,
            Latitude: dto.Latitude,
            Longitude: dto.Longitude,
            ParkingType: dto.ParkingType,
            TotalSpots: dto.TotalSpots,
            HourlyRate: dto.HourlyRate,
            DailyRate: dto.DailyRate,
            WeeklyRate: dto.WeeklyRate,
            MonthlyRate: dto.MonthlyRate,
            OpenTime: dto.OpenTime,
            CloseTime: dto.CloseTime,
            Is24Hours: dto.Is24Hours,
            Amenities: dto.Amenities,
            AllowedVehicleTypes: dto.AllowedVehicleTypes,
            ImageUrls: dto.ImageUrls,
            SpecialInstructions: dto.SpecialInstructions,
            ZoneCode: dto.ZoneCode);

        var result = await _parkingSpaces.UpdateAsync(command.CompanyId, command.ParkingSpaceId, update, ct);
        if (!result.Success || result.Space is null)
            return new ApiResponse<CorporateParkingSpaceDto>(false, result.Message, null);

        await _corporate.SaveChangesAsync(ct);
        await CacheInvalidation.ForParkingMutationAsync(
            _cache, result.Space.Id, result.Space.OwnerId, includeReviews: false, ct);
        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            result.Message,
            CorporateMapping.ToCorporateParkingSpaceDto(result.Space));
    }
}
