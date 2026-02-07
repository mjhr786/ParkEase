using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.Interfaces;
using System.Security.Claims;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/files")]
public class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] AllowedVideoTypes = { "video/mp4", "video/webm" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    private const long MaxVideoSize = 50 * 1024 * 1024; // 50MB

    public FileUploadController(IFileUploadService fileUploadService)
    {
        _fileUploadService = fileUploadService;
    }

    [HttpPost("parking/{parkingSpaceId:guid}/upload")]
    [Authorize(Roles = "Vendor,Admin")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB total request limit
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

        // Validate files
        var validFiles = new List<(Stream Stream, string FileName, string ContentType)>();
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

            validFiles.Add((file.OpenReadStream(), file.FileName, file.ContentType));
        }

        if (validFiles.Count == 0)
        {
            return BadRequest(new { success = false, message = "No valid files to upload", errors });
        }

        try
        {
            var uploadedUrls = await _fileUploadService.UploadParkingImagesAsync(
                parkingSpaceId, userId.Value, validFiles, cancellationToken);

            // Dispose streams
            foreach (var (stream, _, _) in validFiles)
            {
                await stream.DisposeAsync();
            }

            return Ok(new
            {
                success = true,
                message = $"{uploadedUrls.Count} file(s) uploaded successfully",
                data = new { urls = uploadedUrls, errors }
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "Not authorized to upload files for this parking space" });
        }
    }

    [HttpDelete("parking/{parkingSpaceId:guid}/{fileName}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> DeleteParkingFile(Guid parkingSpaceId, string fileName, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _fileUploadService.DeleteParkingFileAsync(parkingSpaceId, userId.Value, fileName, cancellationToken);

        if (!result)
        {
            return NotFound(new { success = false, message = "File not found or unauthorized" });
        }

        return Ok(new { success = true, message = "File deleted successfully" });
    }

    [HttpGet("parking/{parkingSpaceId:guid}")]
    public IActionResult GetParkingFiles(Guid parkingSpaceId)
    {
        var files = _fileUploadService.GetParkingImages(parkingSpaceId);
        return Ok(new { success = true, data = files });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
