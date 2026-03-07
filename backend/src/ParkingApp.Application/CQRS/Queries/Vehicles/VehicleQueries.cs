using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Vehicles;

public record GetMyVehiclesQuery(Guid UserId) : IQuery<ApiResponse<IEnumerable<VehicleDto>>>;

public class GetMyVehiclesQueryHandler : IQueryHandler<GetMyVehiclesQuery, ApiResponse<IEnumerable<VehicleDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyVehiclesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IEnumerable<VehicleDto>>> HandleAsync(GetMyVehiclesQuery query, CancellationToken cancellationToken = default)
    {
        var vehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(query.UserId, cancellationToken);
        
        var dtos = vehicles.Select(v => new VehicleDto(
            v.Id,
            v.UserId,
            v.LicensePlate,
            v.Make,
            v.Model,
            v.Color,
            v.Type,
            v.IsDefault,
            v.CreatedAt
        ));

        return new ApiResponse<IEnumerable<VehicleDto>>(true, null, dtos);
    }
}
