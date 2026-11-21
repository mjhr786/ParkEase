using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.FileUpload;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;

using System.Security.Claims;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/files")]
public class FileUploadController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<FileUploadController> _logger;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedVideoTypes = { "video/mp4", "video/webm" };
    private const long MaxImageSize = 5 * 1024 * 1024;
    private const long MaxVideoSize = 50 * 1024 * 1024;

    public FileUploadController(IDispatcher dispatcher, ILogger<FileUploadController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [HttpPost("parking/{parkingSpaceId:guid}/upload")]
    [Authorize(Roles = "User,Admin")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadParkingFiles(Guid parkingSpaceId, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (files == null || files.Count == 0)
        {
            return BadRequest(new { success = false, message = "No files provided" });
        }

        var validFiles = new List<UploadFilePayload>();
        var errors = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                errors.Add($"{file.FileName}: Empty file");
                continue;
            }

            var isImage = AllowedImageTypes.Contains(file.ContentType.ToLower());
            var isVideo = AllowedVideoTypes.Contains(file.ContentType.ToLower());

            if (!isImage && !isVideo)
            {
                errors.Add($"{file.FileName}: Invalid file type. Allowed: JPG, PNG, WEBP, MP4, WEBM");
                continue;
            }

            if (!ExtensionMatches(file.FileName, isImage))
            {
                errors.Add($"{file.FileName}: File extension does not match the declared type");
                continue;
            }

            var maxSize = isImage ? MaxImageSize : MaxVideoSize;
            if (file.Length > maxSize)
            {
                var maxSizeMb = maxSize / (1024 * 1024);
                errors.Add($"{file.FileName}: File too large. Max {maxSizeMb}MB for {(isImage ? "images" : "videos")}");
                continue;
            }

            var stream = file.OpenReadStream();
            if (!SignatureMatches(stream, isImage))
            {
                await stream.DisposeAsync();
                errors.Add($"{file.FileName}: File content does not match the declared type");
                continue;
            }

            if (!stream.CanSeek)
            {
                await stream.DisposeAsync();
                errors.Add($"{file.FileName}: Unable to read file");
                continue;
            }

            stream.Position = 0;
            validFiles.Add(new UploadFilePayload(stream, file.FileName, file.ContentType));
        }

        if (validFiles.Count == 0)
        {
            return BadRequest(new { success = false, message = "No valid files to upload", errors });
        }

        try
        {
            var result = await _dispatcher.SendAsync(
                new UploadParkingFilesCommand(parkingSpaceId, userId.Value, validFiles),
                cancellationToken);

            foreach (var file in validFiles)
            {
                await file.Stream.DisposeAsync();
            }

            if (!result.Success)
            {
                return Unauthorized(new { success = false, message = result.Message });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new { urls = result.Data!.Urls, errors }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parking file upload failed for space {ParkingSpaceId}", parkingSpaceId);
            foreach (var file in validFiles)
                await file.Stream.DisposeAsync();
            return BadRequest(new { success = false, message = "Upload failed" });
        }
    }

    private static bool ExtensionMatches(string fileName, bool isImage)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return isImage
            ? ext is ".jpg" or ".jpeg" or ".png" or ".webp"
            : ext is ".mp4" or ".webm";
    }

    /// <summary>
    /// Content-Type is client-supplied. Require a matching file signature before storage.
    /// </summary>
    private static bool SignatureMatches(Stream stream, bool isImage)
    {
        Span<byte> header = stackalloc byte[16];
        var read = stream.Read(header);
        if (isImage)
        {
            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;
            if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return true;
            return read >= 12
                && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
        }

        if (read >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            return true;
        return read >= 8
            && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p';
    }

    [HttpDelete("parking/{parkingSpaceId:guid}/{fileName}")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> DeleteParkingFile(Guid parkingSpaceId, string fileName, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.SendAsync(
            new DeleteParkingFileCommand(parkingSpaceId, userId.Value, fileName),
            cancellationToken);

        if (!result.Success || !result.Data)
        {
            return NotFound(new { success = false, message = result.Message ?? "File not found or unauthorized" });
        }

        return Ok(new { success = true, message = "File deleted successfully" });
    }

    [HttpGet("parking/{parkingSpaceId:guid}")]
    public async Task<IActionResult> GetParkingFiles(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetParkingFilesQuery(parkingSpaceId), cancellationToken);
        return Ok(new { success = true, data = result.Data });
    }

    [HttpPost("parking/{parkingSpaceId:guid}/sign-upload")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> GeneratePresignedUrl(Guid parkingSpaceId, [FromBody] GenerateUrlRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new GeneratePresignedUrlCommand(parkingSpaceId, userId.Value, request.FileName, request.ContentType),
            cancellationToken);

        if (!result.Success)
        {
            return result.Message?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true
                ? Unauthorized(new { success = false, message = result.Message })
                : BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                uploadUrl = result.Data!.UploadUrl,
                publicUrl = result.Data.PublicUrl,
                key = result.Data.Key
            }
        });
    }

    [HttpPost("parking/{parkingSpaceId:guid}/confirm-upload")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> ConfirmUpload(Guid parkingSpaceId, [FromBody] ConfirmUploadRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new ConfirmParkingUploadCommand(parkingSpaceId, userId.Value, request.FileUrls),
            cancellationToken);

        if (!result.Success)
        {
            return result.Message?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true
                ? Unauthorized(new { success = false, message = result.Message })
                : BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = "Upload confirmed" });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class GenerateUrlRequest
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
}

public class ConfirmUploadRequest
{
    public required List<string> FileUrls { get; set; }
}
