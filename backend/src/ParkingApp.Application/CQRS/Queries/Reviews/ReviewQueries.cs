using Dapper;
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
    private readonly IUnitOfWork _unitOfWork;

    public GetReviewByIdHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ReviewDto>> HandleAsync(GetReviewByIdQuery query, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(query.ReviewId, cancellationToken);
        return review == null
            ? new ApiResponse<ReviewDto>(false, "Review not found", null)
            : new ApiResponse<ReviewDto>(true, null, review.ToDto());
    }
}

/// <summary>
/// Dapper-optimized: single SQL JOIN with Users table for reviewer name.
/// Avoids EF Include chain and change tracking for read-only review listings.
/// </summary>
public sealed class GetReviewsByParkingSpaceHandler : IQueryHandler<GetReviewsByParkingSpaceQuery, ApiResponse<List<ReviewDto>>>
{
    private readonly ISqlConnectionFactory _sql;
    private readonly ICacheService _cache;

    public GetReviewsByParkingSpaceHandler(ISqlConnectionFactory sql, ICacheService cache)
    {
        _sql = sql;
        _cache = cache;
    }

    public async Task<ApiResponse<List<ReviewDto>>> HandleAsync(GetReviewsByParkingSpaceQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"reviews:parking:{query.ParkingSpaceId}";
        var cached = await _cache.GetAsync<List<ReviewDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<List<ReviewDto>>(true, null, cached);

        const string sql = """
            SELECT
                r."Id", r."UserId",
                CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                r."ParkingSpaceId", r."BookingId",
                r."Rating", r."Title", r."Comment", r."HelpfulCount",
                r."OwnerResponse", r."OwnerResponseAt", r."CreatedAt"
            FROM "Reviews" r
            INNER JOIN "Users" u ON r."UserId" = u."Id"
            WHERE r."ParkingSpaceId" = @ParkingSpaceId AND r."IsDeleted" = FALSE
            ORDER BY r."CreatedAt" DESC
            """;

        using var connection = _sql.CreateConnection();
        var dtos = (await connection.QueryAsync<ReviewDto>(sql, new { query.ParkingSpaceId })).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(10), cancellationToken);
        return new ApiResponse<List<ReviewDto>>(true, null, dtos);
    }
}
