using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.Ancillary;

internal sealed class GetAncillaryServicesForParkingHandler
    : IQueryHandler<GetAncillaryServicesForParkingQuery, ApiResponse<List<ParkingAncillaryServiceDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetAncillaryServicesForParkingHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<ParkingAncillaryServiceDto>>> HandleAsync(
        GetAncillaryServicesForParkingQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = await _unitOfWork.ParkingAncillaryServices.GetByParkingSpaceIdAsync(
            query.ParkingSpaceId,
            query.ActiveOnly,
            cancellationToken);

        var dtos = items.Select(AncillaryServiceResolver.ToDto).ToList();
        return new ApiResponse<List<ParkingAncillaryServiceDto>>(true, null, dtos);
    }
}

internal sealed class GetMyAncillaryServicesHandler
    : IQueryHandler<GetMyAncillaryServicesQuery, ApiResponse<List<ParkingAncillaryServiceDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetMyAncillaryServicesHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<ParkingAncillaryServiceDto>>> HandleAsync(
        GetMyAncillaryServicesQuery query,
        CancellationToken cancellationToken = default)
    {
        var spaces = await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(query.VendorId, cancellationToken);
        var spaceIds = spaces.Select(s => s.Id).ToList();
        if (spaceIds.Count == 0)
            return new ApiResponse<List<ParkingAncillaryServiceDto>>(true, null, new List<ParkingAncillaryServiceDto>());

        var items = await _unitOfWork.ParkingAncillaryServices.GetByParkingSpaceIdsAsync(
            spaceIds,
            activeOnly: false,
            cancellationToken);

        var dtos = items.Select(AncillaryServiceResolver.ToDto).ToList();
        return new ApiResponse<List<ParkingAncillaryServiceDto>>(true, null, dtos);
    }
}
