using ParkingApp.Application.CQRS.Commands.FileUpload.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.FileUpload;

public class ConfirmParkingUploadHandler : ICommandHandler<ConfirmParkingUploadCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public ConfirmParkingUploadHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<bool>> HandleAsync(
        ConfirmParkingUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await ParkingFileUploadHelper.GetOwnedParkingSpaceAsync(
                _unitOfWork, command.ParkingSpaceId, command.UserId, cancellationToken);

            if (command.FileUrls.Count > 0)
            {
                await ParkingFileUploadHelper.AppendParkingImagesAsync(
                    parking, command.FileUrls, _unitOfWork, _cache, cancellationToken);
            }

            return new ApiResponse<bool>(true, "Upload confirmed", true);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}