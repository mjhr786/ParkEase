using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class R2FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly string _bucketName;
    private readonly string _publicUrl;

    public R2FileStorage(IAmazonS3 s3Client, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _bucketName = configuration["Storage:R2:BucketName"] ?? throw new InvalidOperationException("R2 BucketName is missing");
        _publicUrl = configuration["Storage:R2:PublicUrl"]?.TrimEnd('/') ?? throw new InvalidOperationException("R2 PublicUrl is missing");
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);
        return $"{_publicUrl}/{key}";
    }

    public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // Extract key from URL if full URL is passed
        var key = fileName;
        if (fileName.StartsWith(_publicUrl))
        {
            key = fileName.Substring(_publicUrl.Length).TrimStart('/');
        }

        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, key, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Ignore if file already deleted
        }
    }

    public (string UploadUrl, string PublicUrl, string Key) GetPresignedUploadUrl(string fileName, string contentType, TimeSpan expiry)
    {
        var key = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = contentType
        };

        var uploadUrl = _s3Client.GetPreSignedURL(request);
        return (uploadUrl, $"{_publicUrl}/{key}", key);
    }
}
