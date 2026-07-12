using ParkingApp.Application.CQRS.Commands.FileUpload.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.FileUpload;

public class GeneratePresignedUrlHandler : ICommandHandler<GeneratePresignedUrlCommand, ApiResponse<PresignedUploadUrlDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public GeneratePresignedUrlHandler(IMarketplaceUnitOfWork unitOfWork, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<ApiResponse<PresignedUploadUrlDto>> HandleAsync(
        GeneratePresignedUrlCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ParkingFileUploadHelper.GetOwnedParkingSpaceAsync(
                _unitOfWork, command.ParkingSpaceId, command.UserId, cancellationToken);

            var result = _fileStorage.GetPresignedUploadUrl(
                command.FileName, command.ContentType, TimeSpan.FromMinutes(10));

            return new ApiResponse<PresignedUploadUrlDto>(
                true,
                null,
                new PresignedUploadUrlDto(result.UploadUrl, result.PublicUrl, result.Key));
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ApiResponse<PresignedUploadUrlDto>(false, ex.Message, null);
        }
        catch (Exception ex)
        {
            return new ApiResponse<PresignedUploadUrlDto>(false, ex.Message, null);
        }
    }
}