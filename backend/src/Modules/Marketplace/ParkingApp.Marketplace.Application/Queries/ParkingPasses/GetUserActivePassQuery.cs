using ParkingApp.Application.CQRS;
using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.ParkingPasses;

public sealed record GetUserActivePassQuery(Guid UserId)
    : IQuery<ApiResponse<ActiveParkingPassesDto>>;

internal sealed class GetUserActivePassHandler : IQueryHandler<GetUserActivePassQuery, ApiResponse<ActiveParkingPassesDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetUserActivePassHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<ActiveParkingPassesDto>> HandleAsync(GetUserActivePassQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.UserActivePasses(query.UserId);
        var cached = await _cache.GetAsync<ActiveParkingPassesDto>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<ActiveParkingPassesDto>(true, null, cached);

        var now = DateTime.UtcNow;
        var activePasses = await _unitOfWork.ParkingPasses.GetActiveByUserIdAsync(query.UserId, now, cancellationToken);

        var result = new ActiveParkingPassesDto(
            activePasses.Count > 0,
            activePasses.Select(pass => pass.ToDto(now)).ToList());

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return new ApiResponse<ActiveParkingPassesDto>(true, null, result);
    }
}
