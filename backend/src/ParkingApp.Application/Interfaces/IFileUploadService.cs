using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

public interface IFileUploadService
{
    // Server-side upload (Legacy/Fallback)
    Task<List<string>> UploadParkingImagesAsync(Guid parkingSpaceId, Guid ownerId, 
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files, 
        CancellationToken cancellationToken = default);
    
    // Client-side Direct Upload
    Task<(string UploadUrl, string PublicUrl, string Key)> GeneratePresignedUrlAsync(
        Guid parkingSpaceId, Guid ownerId, string fileName, string contentType, CancellationToken cancellationToken = default);
        
    Task ConfirmUploadAsync(Guid parkingSpaceId, Guid ownerId, List<string> newFileUrls, CancellationToken cancellationToken = default);

    Task<bool> DeleteParkingFileAsync(Guid parkingSpaceId, Guid ownerId, string fileUrlOrKey, 
        CancellationToken cancellationToken = default);
    
    // Get images from DB
    Task<List<string>> GetParkingImagesAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
}
