using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Services;

namespace ParkingApp.Marketplace.Infrastructure;

/// <summary>
/// Composition helpers for marketplace file storage (R2 or local disk).
/// Called from the API host so web-root paths and environment stay at the edge.
/// </summary>
public static class FileStorageRegistration
{
    public const string ProviderConfigKey = "Storage:Provider";
    public const string R2ProviderName = "R2";

    /// <summary>
    /// Registers <see cref="IFileStorage"/> using Cloudflare R2 when
    /// <c>Storage:Provider=R2</c>; otherwise local disk under <paramref name="webRootPath"/>/uploads.
    /// </summary>
    public static IServiceCollection AddMarketplaceFileStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string webRootPath,
        string publicBaseUrl)
    {
        if (string.Equals(configuration[ProviderConfigKey], R2ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            var accountId = configuration["Storage:R2:AccountId"]
                ?? throw new InvalidOperationException("Storage:R2:AccountId is required when Storage:Provider=R2.");
            var accessKey = configuration["Storage:R2:AccessKey"]
                ?? throw new InvalidOperationException("Storage:R2:AccessKey is required when Storage:Provider=R2.");
            var secretKey = configuration["Storage:R2:SecretKey"]
                ?? throw new InvalidOperationException("Storage:R2:SecretKey is required when Storage:Provider=R2.");

            var serviceUrl = $"https://{accountId}.r2.cloudflarestorage.com";
            var s3Config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            };

            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(accessKey, secretKey, s3Config));
            services.AddScoped<IFileStorage, R2FileStorage>();
            return services;
        }

        var uploadsRoot = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);
        var baseUrl = $"{publicBaseUrl.TrimEnd('/')}/uploads";

        services.AddScoped<IFileStorage>(_ => new LocalFileStorage(webRootPath, baseUrl));
        return services;
    }

    public static bool IsR2Enabled(IConfiguration configuration) =>
        string.Equals(configuration[ProviderConfigKey], R2ProviderName, StringComparison.OrdinalIgnoreCase);
}
