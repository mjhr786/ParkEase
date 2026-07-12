using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS.Commands.FileUpload;

public record UploadFilePayload(Stream Stream, string FileName, string ContentType);

public record UploadParkingFilesCommand(
    Guid ParkingSpaceId,
    Guid UserId,
    List<UploadFilePayload> Files
) : ICommand<ApiResponse<UploadParkingFilesResultDto>>;

public record GeneratePresignedUrlCommand(
    Guid ParkingSpaceId,
    Guid UserId,
    string FileName,
    string ContentType
) : ICommand<ApiResponse<PresignedUploadUrlDto>>;

public record ConfirmParkingUploadCommand(
    Guid ParkingSpaceId,
    Guid UserId,
    List<string> FileUrls
) : ICommand<ApiResponse<bool>>;

public record DeleteParkingFileCommand(
    Guid ParkingSpaceId,
    Guid UserId,
    string FileUrlOrKey
) : ICommand<ApiResponse<bool>>;

public record GetParkingFilesQuery(
    Guid ParkingSpaceId
) : IQuery<ApiResponse<List<string>>>;