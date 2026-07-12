using ParkingApp.Application.CQRS.Commands.FileUpload.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.FileUpload;

public class DeleteParkingFileHandler : ICommandHandler<DeleteParkingFileCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IFileStorage _fileStorage;

    public DeleteParkingFileHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<bool>> HandleAsync(
        DeleteParkingFileCommand command,
        CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != command.UserId)
        {
            return new ApiResponse<bool>(false, "File not found or unauthorized", false);
        }

        await _fileStorage.DeleteFileAsync(command.FileUrlOrKey, cancellationToken);

        if (!string.IsNullOrEmpty(parking.ImageUrls))
        {
            var urls = parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var urlToRemove = urls.FirstOrDefault(u =>
                u.EndsWith(command.FileUrlOrKey, StringComparison.OrdinalIgnoreCase) ||
                command.FileUrlOrKey.EndsWith(u, StringComparison.OrdinalIgnoreCase));

            if (urlToRemove != null)
            {
                urls.Remove(urlToRemove);
                parking.SetImageUrlsCsv(string.Join(",", urls));

                _unitOfWork.ParkingSpaces.Update(parking);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await ParkingFileUploadHelper.InvalidateParkingCachesAsync(
                    _cache, command.ParkingSpaceId, parking.OwnerId, cancellationToken);

                return new ApiResponse<bool>(true, "File deleted successfully", true);
            }
        }

        return new ApiResponse<bool>(false, "File not found or unauthorized", false);
    }
}