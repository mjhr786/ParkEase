using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http; // For IHttpContextAccessor if needed, but here we just need paths
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class LocalFileStorage : IFileStorage
{
    private readonly string _webRootPath;
    private readonly string _baseUrl; // e.g. https://localhost:5000/uploads/

    public LocalFileStorage(string webRootPath, string baseUrl)
    {
        _webRootPath = webRootPath;
        _baseUrl = baseUrl.TrimEnd('/');
    }
    
    // Fallback for direct upload - simulated
    // Ideally we would return an upload URL to our own API
    public (string UploadUrl, string PublicUrl, string Key) GetPresignedUploadUrl(string fileName, string contentType, TimeSpan expiry)
    {
        // For local dev, we don't support true "direct" upload easily without a specific controller endpoint
        // But we can simulate it by returning a URL that points to our own upload endpoint
        // For now, throw NotSupported or implement a specific local upload endpoint
        throw new NotSupportedException("Direct upload is not supported in Local storage mode. Use standard upload.");
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var uploadsFolder = Path.Combine(_webRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);
        
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return $"/uploads/{uniqueFileName}"; 
    }

    public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var relativePath = fileName.TrimStart('/');
        var filePath = Path.Combine(_webRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }
}
