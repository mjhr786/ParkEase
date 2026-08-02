using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.Reviews;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Queries
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

public sealed record GetReviewByIdQuery(Guid ReviewId) : IQuery<ApiResponse<ReviewDto>>;
public sealed record GetReviewsByParkingSpaceQuery(Guid ParkingSpaceId) : IQuery<ApiResponse<List<ReviewDto>>>;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Handlers
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

/// <summary>
/// Single-record lookup — EF Core is fine here (simple key lookup).
/// </summary>
internal sealed class GetReviewByIdHandler : IQueryHandler<GetReviewByIdQuery, ApiResponse<ReviewDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetReviewByIdHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ReviewDto>> HandleAsync(GetReviewByIdQuery query, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(query.ReviewId, cancellationToken);
        if (review == null)
            return new ApiResponse<ReviewDto>(false, "Review not found", null);

        // KD-9: do not expose reviews for corporate-only inventory on marketplace product APIs.
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(review.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.IsCorporateOnly)
            return new ApiResponse<ReviewDto>(false, "Review not found", null);

        return new ApiResponse<ReviewDto>(true, null, review.ToDto());
    }
}

/// <summary>
/// List reviews for a parking space via <see cref="IReviewReadStore"/> (caching stays here).
/// </summary>
internal sealed class GetReviewsByParkingSpaceHandler : IQueryHandler<GetReviewsByParkingSpaceQuery, ApiResponse<List<ReviewDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IReviewReadStore _readStore;
    private readonly ICacheService _cache;

    public GetReviewsByParkingSpaceHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IReviewReadStore readStore,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _readStore = readStore;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ReviewDto>>> HandleAsync(GetReviewsByParkingSpaceQuery query, CancellationToken cancellationToken = default)
    {
        // KD-9: hide reviews for corporate-only inventory (404-equivalent empty product surface).
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.IsCorporateOnly)
            return new ApiResponse<List<ReviewDto>>(true, null, new List<ReviewDto>());

        var cacheKey = CacheKeys.Reviews(query.ParkingSpaceId);
        var cached = await _cache.GetAsync<List<ReviewDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ReviewDto>>(true, null, cached);

        var dtos = (await _readStore.GetByParkingSpaceAsync(query.ParkingSpaceId, cancellationToken)).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10), cancellationToken);
        return new ApiResponse<List<ReviewDto>>(true, null, dtos);
    }
}
