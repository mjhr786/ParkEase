using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class FileUploadService : IFileUploadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IFileStorage _fileStorage;

    public FileUploadService(IUnitOfWork unitOfWork, ICacheService cache, IFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _fileStorage = fileStorage;
    }

    public async Task<List<string>> UploadParkingImagesAsync(
        Guid parkingSpaceId, 
        Guid ownerId,
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files,
        CancellationToken cancellationToken = default)
    {
        // Verify ownership
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("Unauthorized to upload files for this parking space");
        }

        var uploadedUrls = new List<string>();

        foreach (var (stream, fileName, contentType) in files)
        {
            var url = await _fileStorage.UploadFileAsync(stream, fileName, contentType, cancellationToken);
            uploadedUrls.Add(url);
        }

        await UpdateParkingImagesAsync(parking, uploadedUrls, cancellationToken);

        return uploadedUrls;
    }

    public async Task<(string UploadUrl, string PublicUrl, string Key)> GeneratePresignedUrlAsync(
        Guid parkingSpaceId, Guid ownerId, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        // Verify ownership
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("Unauthorized to upload files for this parking space");
        }

        // Generate URL (valid for 10 minutes)
        return _fileStorage.GetPresignedUploadUrl(fileName, contentType, TimeSpan.FromMinutes(10));
    }

    public async Task ConfirmUploadAsync(Guid parkingSpaceId, Guid ownerId, List<string> newFileUrls, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
             throw new UnauthorizedAccessException("Unauthorized to update this parking space");
        }

        if (newFileUrls != null && newFileUrls.Count > 0)
        {
            await UpdateParkingImagesAsync(parking, newFileUrls, cancellationToken);
        }
    }

    private async Task UpdateParkingImagesAsync(ParkingApp.Domain.Entities.ParkingSpace parking, List<string> newUrls, CancellationToken cancellationToken)
    {
        var existingUrls = string.IsNullOrEmpty(parking.ImageUrls) 
            ? new List<string>() 
            : parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        existingUrls.AddRange(newUrls);
        
        // Dedup just in case
        parking.ImageUrls = string.Join(",", existingUrls.Distinct());
        
        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await _cache.RemoveAsync($"parking:{parking.Id}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
    }

    public async Task<bool> DeleteParkingFileAsync(
        Guid parkingSpaceId, 
        Guid ownerId, 
        string fileUrlOrKey,
        CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            return false;
        }

        // Delete from storage
        await _fileStorage.DeleteFileAsync(fileUrlOrKey, cancellationToken);
        
        // Update DB
        if (!string.IsNullOrEmpty(parking.ImageUrls))
        {
            var urls = parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            
            // Remove by matching URL or Key (simplistic match)
            // Assuming fileUrlOrKey is the full Public URL
            var urlToRemove = urls.FirstOrDefault(u => u.EndsWith(fileUrlOrKey) || fileUrlOrKey.EndsWith(u));
            
            if (urlToRemove != null)
            {
                urls.Remove(urlToRemove);
                parking.ImageUrls = string.Join(",", urls);
                
                _unitOfWork.ParkingSpaces.Update(parking);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Invalidate caches
                await _cache.RemoveAsync($"parking:{parkingSpaceId}", cancellationToken);
                await _cache.RemoveByPatternAsync("search:*", cancellationToken);
                return true;
            }
        }

        return false;
    }

    public async Task<List<string>> GetParkingImagesAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || string.IsNullOrEmpty(parking.ImageUrls))
        {
            return new List<string>();
        }

        return parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
