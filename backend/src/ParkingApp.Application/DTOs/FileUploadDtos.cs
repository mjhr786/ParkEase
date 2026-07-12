namespace ParkingApp.Application.DTOs;

public record UploadParkingFilesResultDto(List<string> Urls, List<string> Errors);

public record PresignedUploadUrlDto(string UploadUrl, string PublicUrl, string Key);