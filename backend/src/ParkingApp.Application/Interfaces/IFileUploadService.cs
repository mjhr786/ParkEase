namespace ParkingApp.Application.Interfaces;

public interface IFileUploadService
{
    Task<List<string>> UploadParkingImagesAsync(Guid parkingSpaceId, Guid ownerId, 
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files, 
        CancellationToken cancellationToken = default);
    
    Task<bool> DeleteParkingFileAsync(Guid parkingSpaceId, Guid ownerId, string fileName, 
        CancellationToken cancellationToken = default);
    
    List<string> GetParkingImages(Guid parkingSpaceId);
}
