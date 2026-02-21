using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Reviews;

// ────────────────────────────────────────────────────────────────
// Commands
// ────────────────────────────────────────────────────────────────

public sealed record CreateReviewCommand(Guid UserId, CreateReviewDto Dto) : ICommand<ApiResponse<ReviewDto>>;
public sealed record UpdateReviewCommand(Guid ReviewId, Guid UserId, UpdateReviewDto Dto) : ICommand<ApiResponse<ReviewDto>>;
public sealed record DeleteReviewCommand(Guid ReviewId, Guid UserId) : ICommand<ApiResponse<bool>>;
public sealed record AddOwnerResponseCommand(Guid ReviewId, Guid OwnerId, OwnerResponseDto Dto) : ICommand<ApiResponse<ReviewDto>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class CreateReviewHandler : ICommandHandler<CreateReviewCommand, ApiResponse<ReviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<CreateReviewHandler> _logger;

    public CreateReviewHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<CreateReviewHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<ReviewDto>> HandleAsync(CreateReviewCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, cancellationToken);
        if (parking == null)
            return new ApiResponse<ReviewDto>(false, "Parking space not found", null);

        if (command.Dto.BookingId.HasValue)
        {
            var booking = await _unitOfWork.Bookings.GetByIdAsync(command.Dto.BookingId.Value, cancellationToken);
            if (booking == null || booking.UserId != command.UserId || booking.Status != BookingStatus.Completed)
                return new ApiResponse<ReviewDto>(false, "Invalid booking reference", null);
        }

        var existingReview = await _unitOfWork.Reviews.FirstOrDefaultAsync(
            r => r.UserId == command.UserId && r.ParkingSpaceId == command.Dto.ParkingSpaceId, cancellationToken);
        if (existingReview != null)
            return new ApiResponse<ReviewDto>(false, "You have already reviewed this parking space", null);

        var review = new Review
        {
            UserId = command.UserId,
            ParkingSpaceId = command.Dto.ParkingSpaceId,
            BookingId = command.Dto.BookingId,
            Rating = command.Dto.Rating,
            Title = command.Dto.Title?.Trim(),
            Comment = command.Dto.Comment?.Trim()
        };

        await _unitOfWork.Reviews.AddAsync(review, cancellationToken);

        // Update parking space rating
        parking.TotalReviews++;
        var newAverage = await _unitOfWork.Reviews.GetAverageRatingAsync(command.Dto.ParkingSpaceId, cancellationToken);
        parking.AverageRating = ((newAverage * (parking.TotalReviews - 1)) + command.Dto.Rating) / parking.TotalReviews;
        _unitOfWork.ParkingSpaces.Update(parking);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync($"reviews:parking:{command.Dto.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveAsync($"parking:{command.Dto.ParkingSpaceId}", cancellationToken);

        _logger.LogInformation("Review submitted for parking {ParkingSpaceId} by user {UserId}, rating: {Rating}",
            command.Dto.ParkingSpaceId, command.UserId, command.Dto.Rating);

        return new ApiResponse<ReviewDto>(true, "Review submitted", review.ToDto());
    }
}

public sealed class UpdateReviewHandler : ICommandHandler<UpdateReviewCommand, ApiResponse<ReviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public UpdateReviewHandler(IUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<ReviewDto>> HandleAsync(UpdateReviewCommand command, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review == null) return new ApiResponse<ReviewDto>(false, "Review not found", null);
        if (review.UserId != command.UserId) return new ApiResponse<ReviewDto>(false, "Unauthorized", null);

        var oldRating = review.Rating;
        if (command.Dto.Rating.HasValue) review.Rating = command.Dto.Rating.Value;
        if (command.Dto.Title != null) review.Title = command.Dto.Title.Trim();
        if (command.Dto.Comment != null) review.Comment = command.Dto.Comment.Trim();

        _unitOfWork.Reviews.Update(review);

        if (command.Dto.Rating.HasValue && command.Dto.Rating.Value != oldRating)
        {
            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(review.ParkingSpaceId, cancellationToken);
            if (parking != null && parking.TotalReviews > 0)
            {
                var currentTotal = parking.AverageRating * parking.TotalReviews;
                parking.AverageRating = (currentTotal - oldRating + command.Dto.Rating.Value) / parking.TotalReviews;
                _unitOfWork.ParkingSpaces.Update(parking);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"reviews:parking:{review.ParkingSpaceId}", cancellationToken);
        if (command.Dto.Rating.HasValue && command.Dto.Rating.Value != oldRating)
            await _cache.RemoveAsync($"parking:{review.ParkingSpaceId}", cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Review updated", review.ToDto());
    }
}

public sealed class DeleteReviewHandler : ICommandHandler<DeleteReviewCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteReviewHandler(IUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteReviewCommand command, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review == null) return new ApiResponse<bool>(false, "Review not found", false);
        if (review.UserId != command.UserId) return new ApiResponse<bool>(false, "Unauthorized", false);

        var parkingSpaceId = review.ParkingSpaceId;
        var rating = review.Rating;

        _unitOfWork.Reviews.Remove(review);

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking != null && parking.TotalReviews > 0)
        {
            if (parking.TotalReviews == 1)
            {
                parking.AverageRating = 0;
                parking.TotalReviews = 0;
            }
            else
            {
                var currentTotal = parking.AverageRating * parking.TotalReviews;
                parking.TotalReviews--;
                parking.AverageRating = (currentTotal - rating) / parking.TotalReviews;
            }
            _unitOfWork.ParkingSpaces.Update(parking);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"reviews:parking:{parkingSpaceId}", cancellationToken);
        await _cache.RemoveAsync($"parking:{parkingSpaceId}", cancellationToken);

        return new ApiResponse<bool>(true, "Review deleted", true);
    }
}

public sealed class AddOwnerResponseHandler : ICommandHandler<AddOwnerResponseCommand, ApiResponse<ReviewDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public AddOwnerResponseHandler(IUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<ReviewDto>> HandleAsync(AddOwnerResponseCommand command, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(command.ReviewId, cancellationToken);
        if (review == null) return new ApiResponse<ReviewDto>(false, "Review not found", null);

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(review.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != command.OwnerId)
            return new ApiResponse<ReviewDto>(false, "Unauthorized", null);

        review.OwnerResponse = command.Dto.Response.Trim();
        review.OwnerResponseAt = DateTime.UtcNow;

        _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"reviews:parking:{review.ParkingSpaceId}", cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Response added", review.ToDto());
    }
}
