using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.Favorites;

public sealed record GetMyFavoritesQuery(Guid UserId) : IQuery<ApiResponse<IEnumerable<ParkingSpaceDto>>>;

internal sealed class GetMyFavoritesQueryHandler : IQueryHandler<GetMyFavoritesQuery, ApiResponse<IEnumerable<ParkingSpaceDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetMyFavoritesQueryHandler(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IEnumerable<ParkingSpaceDto>>> HandleAsync(GetMyFavoritesQuery query, CancellationToken cancellationToken = default)
    {
        var favorites = await _unitOfWork.Favorites.GetByUserIdAsync(query.UserId, cancellationToken);
        
        var dtos = favorites.Select(f => f.ParkingSpace.ToDto());
        
        return new ApiResponse<IEnumerable<ParkingSpaceDto>>(true, "Favorites retrieved successfully", dtos);
    }
}
