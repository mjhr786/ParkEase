using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.FileUpload;
using ParkingApp.Application.DTOs;
using System.Security.Claims;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/files")]
public class FileUploadController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedVideoTypes = { "video/mp4", "video/webm" };
    private const long MaxImageSize = 5 * 1024 * 1024;
    private const long MaxVideoSize = 50 * 1024 * 1024;

    public FileUploadController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
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

            var maxSize = isImage ? MaxImageSize : MaxVideoSize;
            if (file.Length > maxSize)
            {
                var maxSizeMb = maxSize / (1024 * 1024);
                errors.Add($"{file.FileName}: File too large. Max {maxSizeMb}MB for {(isImage ? "images" : "videos")}");
                continue;
            }

            validFiles.Add(new UploadFilePayload(file.OpenReadStream(), file.FileName, file.ContentType));
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
            return BadRequest(new { success = false, message = ex.Message });
        }
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