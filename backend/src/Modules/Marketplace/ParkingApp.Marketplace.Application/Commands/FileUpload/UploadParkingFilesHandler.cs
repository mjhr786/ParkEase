using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.FileUpload.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.FileUpload;

internal sealed class UploadParkingFilesHandler : ICommandHandler<UploadParkingFilesCommand, ApiResponse<UploadParkingFilesResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IFileStorage _fileStorage;

    public UploadParkingFilesHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<UploadParkingFilesResultDto>> HandleAsync(
        UploadParkingFilesCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await ParkingFileUploadHelper.GetOwnedParkingSpaceAsync(
                _unitOfWork, command.ParkingSpaceId, command.UserId, cancellationToken);

            var uploadedUrls = new List<string>();

            foreach (var file in command.Files)
            {
                var url = await _fileStorage.UploadFileAsync(file.Stream, file.FileName, file.ContentType, cancellationToken);
                uploadedUrls.Add(url);
            }

            await ParkingFileUploadHelper.AppendParkingImagesAsync(
                parking, uploadedUrls, _unitOfWork, _cache, cancellationToken);

            return new ApiResponse<UploadParkingFilesResultDto>(
                true,
                $"{uploadedUrls.Count} file(s) uploaded successfully",
                new UploadParkingFilesResultDto(uploadedUrls, new List<string>()));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApiResponse<UploadParkingFilesResultDto>(false, ex.Message, null);
        }
    }
}
