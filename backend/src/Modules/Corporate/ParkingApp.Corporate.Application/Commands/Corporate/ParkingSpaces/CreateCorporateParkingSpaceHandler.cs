using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

internal sealed class CreateCorporateParkingSpaceHandler
    : ICommandHandler<CreateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly ICompanyOwnedParkingSpaceService _parkingSpaces;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public CreateCorporateParkingSpaceHandler(
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
        CreateCorporateParkingSpaceCommand command,
        CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company is null)
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company not found.", null);

        var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership is null || !adminMembership.IsActive || !adminMembership.IsAdmin)
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can create company-owned parking.", null);

        var create = await _parkingSpaces.CreateAsync(command.CompanyId, command.AdminUserId, command.Dto, ct);
        if (!create.Success || create.Space is null)
            return new ApiResponse<CorporateParkingSpaceDto>(false, create.Message, null);

        await _corporate.SaveChangesAsync(ct);
        await CacheInvalidation.ForParkingMutationAsync(
            _cache, create.Space.Id, create.Space.OwnerId, includeReviews: false, ct);
        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            create.Message,
            CorporateMapping.ToCorporateParkingSpaceDto(create.Space));
    }
}
