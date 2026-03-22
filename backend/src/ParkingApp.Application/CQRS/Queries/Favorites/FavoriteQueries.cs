using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Favorites;

public sealed record GetMyFavoritesQuery(Guid UserId) : IQuery<ApiResponse<IEnumerable<ParkingSpaceDto>>>;

public sealed class GetMyFavoritesQueryHandler : IQueryHandler<GetMyFavoritesQuery, ApiResponse<IEnumerable<ParkingSpaceDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyFavoritesQueryHandler(IUnitOfWork unitOfWork)
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
