using System.ComponentModel.DataAnnotations;

namespace ParkingApp.Marketplace.Contracts.DTOs;

public record ReviewDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid ParkingSpaceId,
    Guid? BookingId,
    int Rating,
    string? Title,
    string? Comment,
    int HelpfulCount,
    string? OwnerResponse,
    DateTime? OwnerResponseAt,
    DateTime CreatedAt
);

public record CreateReviewDto(
    [Required] Guid ParkingSpaceId,
    Guid? BookingId,
    [Required][Range(1, 5)] int Rating,
    string? Title,
    string? Comment
);

public record UpdateReviewDto(
    [Range(1, 5)] int? Rating,
    string? Title,
    string? Comment
);

public record OwnerResponseDto(
    [Required] string Response
);

