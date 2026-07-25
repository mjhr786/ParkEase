using SkiaSharp;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using ParkingApp.API.Options;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Optional on-the-fly resize for <b>local</b> <c>/uploads</c> images only (<c>?w=</c>/<c>?h=</c>).
/// Production free-tier deployments use Cloudflare R2 public URLs — those never hit this middleware.
/// When <see cref="MediaOptions.EnableRuntimeResize"/> is false (default), this is a pure no-op.
/// </summary>
public class ImageResizingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ImageResizingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IOptionsMonitor<MediaOptions> _mediaOptions;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;
    private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public ImageResizingMiddleware(
        RequestDelegate next,
        ILogger<ImageResizingMiddleware> logger,
        IWebHostEnvironment env,
        IOptionsMonitor<MediaOptions> mediaOptions)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _mediaOptions = mediaOptions;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Free-tier / R2 default: skip all work (no Skia decode, no disk thumb cache).
        if (!_mediaOptions.CurrentValue.EnableRuntimeResize)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        // Only local uploads — R2 public URLs are absolute and never match /uploads.
        if (!path.StartsWithSegments("/uploads") || !HasResizeQuery(context.Request.Query))
        {
            await _next(context);
            return;
        }

        var extension = Path.GetExtension(path.Value!).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            await _next(context);
            return;
        }

        var width = GetQueryInt(context.Request.Query, "w");
        var height = GetQueryInt(context.Request.Query, "h");

        if (width == null && height == null)
        {
            await _next(context);
            return;
        }

        // Cap dimensions when resize is enabled (abuse / free-tier CPU protection).
        var maxDim = Math.Clamp(_mediaOptions.CurrentValue.MaxRuntimeResizeDimension, 64, 2048);
        if (width.HasValue)
            width = Math.Min(width.Value, maxDim);
        if (height.HasValue)
            height = Math.Min(height.Value, maxDim);

        var webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var originalPath = Path.Combine(webRootPath, path.Value!.TrimStart('/'));

        if (!File.Exists(originalPath))
        {
            await _next(context); // Let StaticFileMiddleware handle 404 or pass through
            return;
        }

        try
        {
            var resizedPath = GetResizedPath(webRootPath, path.Value, width, height);

            if (!File.Exists(resizedPath))
            {
                await ResizeAndSaveImageAsync(originalPath, resizedPath, width, height);
            }

            if (File.Exists(resizedPath))
            {
                context.Response.ContentType = GetContentType(resizedPath);
                // Cache for 1 year
                context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
                await context.Response.SendFileAsync(resizedPath);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing image {Path}", path);
        }

        await _next(context);
    }

    private static bool HasResizeQuery(IQueryCollection query)
    {
        return query.ContainsKey("w") || query.ContainsKey("h");
    }

    private static int? GetQueryInt(IQueryCollection query, string key)
    {
        if (query.TryGetValue(key, out var value) && int.TryParse(value, out var result) && result > 0)
        {
            return result;
        }
        return null;
    }

    private string GetResizedPath(string webRootPath, string originalUrl, int? width, int? height)
    {
        var directory = Path.GetDirectoryName(originalUrl)?.TrimStart('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(originalUrl);
        var extension = Path.GetExtension(originalUrl);
        var sizeSuffix = $"{(width.HasValue ? $"w{width}" : "")}{(height.HasValue ? $"h{height}" : "")}";

        // Cache in a _resized folder to keep things clean
        // e.g. wwwroot/uploads/parking/123/_resized/image_w200.jpg
        var cacheDir = Path.Combine(webRootPath, directory!, "_resized");
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        return Path.Combine(cacheDir, $"{fileName}_{sizeSuffix}{extension}");
    }

    private async Task ResizeAndSaveImageAsync(string originalPath, string resizedPath, int? width, int? height)
    {
        await Task.Run(() =>
        {
            using var originalStream = File.OpenRead(originalPath);
            using var inputStream = new SKManagedStream(originalStream);
            using var originalBitmap = SKBitmap.Decode(inputStream);

            if (originalBitmap == null) return;

            int newWidth = originalBitmap.Width;
            int newHeight = originalBitmap.Height;

            if (width.HasValue && height.HasValue)
            {
                newWidth = width.Value;
                newHeight = height.Value;
            }
            else if (width.HasValue)
            {
                newWidth = width.Value;
                var ratio = (double)width.Value / originalBitmap.Width;
                newHeight = (int)(originalBitmap.Height * ratio);
            }
            else if (height.HasValue)
            {
                newHeight = height.Value;
                var ratio = (double)height.Value / originalBitmap.Height;
                newWidth = (int)(originalBitmap.Width * ratio);
            }

            // Use SKSamplingOptions instead of obsolete SKFilterQuality
            var samplingOptions = new SKSamplingOptions(SKCubicResampler.Mitchell);
            using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), samplingOptions);

            if (resizedBitmap == null) return;

            using var image = SKImage.FromBitmap(resizedBitmap);
            using var outputStream = File.OpenWrite(resizedPath);

            var format = GetEncodedImageFormat(Path.GetExtension(originalPath));
            image.Encode(format, 80).SaveTo(outputStream);
        });
    }

    private SKEncodedImageFormat GetEncodedImageFormat(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            ".jpeg" or ".jpg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Jpeg
        };
    }

    private string GetContentType(string path)
    {
        if (_contentTypeProvider.TryGetContentType(path, out var contentType))
        {
            return contentType;
        }
        return "application/octet-stream";
    }
}
