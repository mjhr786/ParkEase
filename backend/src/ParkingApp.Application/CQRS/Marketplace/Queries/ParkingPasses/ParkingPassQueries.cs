using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.ParkingPasses;

public sealed record GetUserActivePassQuery(Guid UserId)
    : IQuery<ApiResponse<ActiveParkingPassesDto>>;

public sealed class GetUserActivePassHandler : IQueryHandler<GetUserActivePassQuery, ApiResponse<ActiveParkingPassesDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetUserActivePassHandler(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<ActiveParkingPassesDto>> HandleAsync(GetUserActivePassQuery query, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activePasses = await _unitOfWork.ParkingPasses.GetActiveByUserIdAsync(query.UserId, now, cancellationToken);

        var result = new ActiveParkingPassesDto(
            activePasses.Count > 0,
            activePasses.Select(pass => pass.ToDto(now)).ToList());

        return new ApiResponse<ActiveParkingPassesDto>(true, null, result);
    }
}
