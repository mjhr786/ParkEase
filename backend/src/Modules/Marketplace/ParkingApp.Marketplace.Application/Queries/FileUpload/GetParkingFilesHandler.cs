using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.FileUpload;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.FileUpload;

internal sealed class GetParkingFilesHandler : IQueryHandler<GetParkingFilesQuery, ApiResponse<List<string>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetParkingFilesHandler(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<string>>> HandleAsync(
        GetParkingFilesQuery query,
        CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        // KD-9: corporate-only inventory is not readable via marketplace product file APIs.
        if (parking == null || parking.IsCorporateOnly || string.IsNullOrEmpty(parking.ImageUrls))
        {
            return new ApiResponse<List<string>>(true, null, new List<string>());
        }

        var files = parking.ImageUrls
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return new ApiResponse<List<string>>(true, null, files);
    }
}
