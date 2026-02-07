using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class FileUploadService : IFileUploadService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _uploadsPath;
    
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    private const long MaxVideoSize = 50 * 1024 * 1024; // 50MB

    public FileUploadService(IUnitOfWork unitOfWork, string uploadsPath)
    {
        _unitOfWork = unitOfWork;
        _uploadsPath = uploadsPath;
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
        var parkingDir = Path.Combine(_uploadsPath, "parking", parkingSpaceId.ToString());
        
        // Ensure directory exists
        Directory.CreateDirectory(parkingDir);

        foreach (var (stream, fileName, contentType) in files)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            // Validate file type
            bool isImage = AllowedImageExtensions.Contains(extension);
            bool isVideo = AllowedVideoExtensions.Contains(extension);
            
            if (!isImage && !isVideo)
            {
                continue; // Skip invalid file types
            }

            // Validate file size
            long maxSize = isImage ? MaxImageSize : MaxVideoSize;
            if (stream.Length > maxSize)
            {
                continue; // Skip files that are too large
            }

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(parkingDir, uniqueFileName);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            // Generate relative URL
            var relativeUrl = $"/uploads/parking/{parkingSpaceId}/{uniqueFileName}";
            uploadedUrls.Add(relativeUrl);
        }

        // Update parking space image URLs
        if (uploadedUrls.Count > 0)
        {
            var existingUrls = string.IsNullOrEmpty(parking.ImageUrls) 
                ? new List<string>() 
                : parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            
            existingUrls.AddRange(uploadedUrls);
            parking.ImageUrls = string.Join(",", existingUrls);
            
            _unitOfWork.ParkingSpaces.Update(parking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return uploadedUrls;
    }

    public async Task<bool> DeleteParkingFileAsync(
        Guid parkingSpaceId, 
        Guid ownerId, 
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Verify ownership
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            return false;
        }

        var filePath = Path.Combine(_uploadsPath, "parking", parkingSpaceId.ToString(), fileName);
        
        if (!File.Exists(filePath))
        {
            return false;
        }

        // Delete file
        File.Delete(filePath);

        // Update parking space image URLs
        var relativeUrl = $"/uploads/parking/{parkingSpaceId}/{fileName}";
        if (!string.IsNullOrEmpty(parking.ImageUrls))
        {
            var urls = parking.ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            urls.Remove(relativeUrl);
            parking.ImageUrls = string.Join(",", urls);
            
            _unitOfWork.ParkingSpaces.Update(parking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public List<string> GetParkingImages(Guid parkingSpaceId)
    {
        var parkingDir = Path.Combine(_uploadsPath, "parking", parkingSpaceId.ToString());
        
        if (!Directory.Exists(parkingDir))
        {
            return new List<string>();
        }

        return Directory.GetFiles(parkingDir)
            .Select(f => $"/uploads/parking/{parkingSpaceId}/{Path.GetFileName(f)}")
            .ToList();
    }
}
