using ParkingApp.Application.CQRS.Commands.FileUpload;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.FileUpload;

public class GetParkingFilesHandler : IQueryHandler<GetParkingFilesQuery, ApiResponse<List<string>>>
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
        if (parking == null || string.IsNullOrEmpty(parking.ImageUrls))
        {
            return new ApiResponse<List<string>>(true, null, new List<string>());
        }

        var files = parking.ImageUrls
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return new ApiResponse<List<string>>(true, null, files);
    }
}