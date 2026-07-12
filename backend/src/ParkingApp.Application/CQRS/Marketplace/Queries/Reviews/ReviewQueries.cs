using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Reviews;

// ────────────────────────────────────────────────────────────────
// Queries
// ────────────────────────────────────────────────────────────────

public sealed record GetReviewByIdQuery(Guid ReviewId) : IQuery<ApiResponse<ReviewDto>>;
public sealed record GetReviewsByParkingSpaceQuery(Guid ParkingSpaceId) : IQuery<ApiResponse<List<ReviewDto>>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Single-record lookup — EF Core is fine here (simple key lookup).
/// </summary>
public sealed class GetReviewByIdHandler : IQueryHandler<GetReviewByIdQuery, ApiResponse<ReviewDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetReviewByIdHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ReviewDto>> HandleAsync(GetReviewByIdQuery query, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(query.ReviewId, cancellationToken);
        return review == null
            ? new ApiResponse<ReviewDto>(false, "Review not found", null)
            : new ApiResponse<ReviewDto>(true, null, review.ToDto());
    }
}

/// <summary>
/// List reviews for a parking space via <see cref="IReviewReadStore"/> (caching stays here).
/// </summary>
public sealed class GetReviewsByParkingSpaceHandler : IQueryHandler<GetReviewsByParkingSpaceQuery, ApiResponse<List<ReviewDto>>>
{
    private readonly IReviewReadStore _readStore;
    private readonly ICacheService _cache;

    public GetReviewsByParkingSpaceHandler(IReviewReadStore readStore, ICacheService cache)
    {
        _readStore = readStore;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ReviewDto>>> HandleAsync(GetReviewsByParkingSpaceQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Reviews(query.ParkingSpaceId);
        var cached = await _cache.GetAsync<List<ReviewDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ReviewDto>>(true, null, cached);

        var dtos = (await _readStore.GetByParkingSpaceAsync(query.ParkingSpaceId, cancellationToken)).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10), cancellationToken);
        return new ApiResponse<List<ReviewDto>>(true, null, dtos);
    }
}
