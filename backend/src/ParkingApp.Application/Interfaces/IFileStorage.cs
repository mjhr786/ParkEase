using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.Application.Interfaces;

public interface IFileStorage
{
    /// <summary>
    /// Uploads a file from the server to the storage provider.
    /// Returns the public URL of the uploaded file.
    /// </summary>
    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from the storage provider.
    /// </summary>
    Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a Pre-signed URL for client-side direct uploads.
    /// Returns (UploadUrl, PublicUrl, Key).
    /// </summary>
    (string UploadUrl, string PublicUrl, string Key) GetPresignedUploadUrl(string fileName, string contentType, TimeSpan expiry);
}
