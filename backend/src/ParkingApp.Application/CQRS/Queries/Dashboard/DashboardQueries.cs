using Dapper;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Queries.Dashboard;

// ────────────────────────────────────────────────────────────────
// Queries
// ────────────────────────────────────────────────────────────────

public sealed record GetVendorDashboardQuery(Guid VendorId) : IQuery<ApiResponse<VendorDashboardDto>>;
public sealed record GetMemberDashboardQuery(Guid MemberId) : IQuery<ApiResponse<MemberDashboardDto>>;

// ────────────────────────────────────────────────────────────────
// Handlers (Dapper for read-side performance)
// ────────────────────────────────────────────────────────────────

public sealed class GetVendorDashboardHandler : IQueryHandler<GetVendorDashboardQuery, ApiResponse<VendorDashboardDto>>
{
    private readonly ISqlConnectionFactory _sql;
    private readonly ICacheService _cache;
    private readonly ILogger<GetVendorDashboardHandler> _logger;

    public GetVendorDashboardHandler(ISqlConnectionFactory sql, ICacheService cache, ILogger<GetVendorDashboardHandler> logger)
    {
        _sql = sql;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<VendorDashboardDto>> HandleAsync(GetVendorDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:vendor:{query.VendorId}";
        var cached = await _cache.GetAsync<VendorDashboardDto>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<VendorDashboardDto>(true, null, cached);

        _logger.LogInformation("Generating vendor dashboard for vendor {VendorId}", query.VendorId);

        var now = DateTime.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Single aggregation query: replaces N+1 loop ──
        const string aggregateSql = """
            SELECT
                COUNT(DISTINCT ps."Id") FILTER (WHERE TRUE)                                                    AS "TotalParkingSpaces",
                COUNT(DISTINCT ps."Id") FILTER (WHERE ps."IsActive" = TRUE)                                    AS "ActiveParkingSpaces",
                COALESCE(AVG(ps."AverageRating"), 0)                                                           AS "AverageRating",
                COALESCE(SUM(ps."TotalReviews"), 0)                                                            AS "TotalReviews",
                COUNT(b."Id")                                                                                  AS "TotalBookings",
                COUNT(b."Id") FILTER (WHERE b."Status" = 3)                                                    AS "ActiveBookings",
                COUNT(b."Id") FILTER (WHERE b."Status" = 0)                                                    AS "PendingBookings",
                COUNT(b."Id") FILTER (WHERE b."Status" = 4)                                                    AS "CompletedBookings",
                COALESCE(SUM(b."TotalAmount") FILTER (WHERE b."Status" = 4 AND p."Status" = 1), 0)             AS "TotalEarnings",
                COALESCE(SUM(b."TotalAmount") FILTER (WHERE b."Status" = 4 AND p."Status" = 1
                    AND b."CheckOutTime" >= @StartOfMonth), 0)                                                 AS "MonthlyEarnings",
                COALESCE(SUM(b."TotalAmount") FILTER (WHERE b."Status" = 4 AND p."Status" = 1
                    AND b."CheckOutTime" >= @StartOfWeek), 0)                                                  AS "WeeklyEarnings"
            FROM "ParkingSpaces" ps
            LEFT JOIN "Bookings" b ON b."ParkingSpaceId" = ps."Id" AND b."IsDeleted" = FALSE
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE ps."OwnerId" = @VendorId AND ps."IsDeleted" = FALSE
            """;

        // ── Earnings chart: last 7 days ──
        const string chartSql = """
            SELECT
                TO_CHAR(d.day, 'Dy')                                                    AS "Label",
                COALESCE(SUM(b."TotalAmount") FILTER (WHERE b."Status" = 4 AND p."Status" = 1), 0) AS "Amount"
            FROM generate_series(CURRENT_DATE - INTERVAL '6 days', CURRENT_DATE, '1 day') AS d(day)
            LEFT JOIN "ParkingSpaces" ps ON ps."OwnerId" = @VendorId AND ps."IsDeleted" = FALSE
            LEFT JOIN "Bookings" b ON b."ParkingSpaceId" = ps."Id" AND b."IsDeleted" = FALSE
                AND b."CheckOutTime"::date = d.day
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            GROUP BY d.day
            ORDER BY d.day
            """;

        // ── Recent 5 bookings ──
        const string recentSql = """
            SELECT
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", p."Status" AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE ps."OwnerId" = @VendorId AND b."IsDeleted" = FALSE AND ps."IsDeleted" = FALSE
            ORDER BY b."CreatedAt" DESC
            LIMIT 5
            """;

        using var connection = _sql.CreateConnection();

        var aggregate = await connection.QuerySingleAsync<VendorAggregateRow>(aggregateSql,
            new { query.VendorId, StartOfMonth = startOfMonth, StartOfWeek = startOfWeek });

        var earningsChart = (await connection.QueryAsync<EarningsChartDataDto>(chartSql,
            new { query.VendorId })).ToList();

        var recentBookings = (await connection.QueryAsync<BookingDto>(recentSql,
            new { query.VendorId })).ToList();

        var dashboard = new VendorDashboardDto(
            TotalParkingSpaces: aggregate.TotalParkingSpaces,
            ActiveParkingSpaces: aggregate.ActiveParkingSpaces,
            TotalBookings: aggregate.TotalBookings,
            ActiveBookings: aggregate.ActiveBookings,
            PendingBookings: aggregate.PendingBookings,
            CompletedBookings: aggregate.CompletedBookings,
            TotalEarnings: aggregate.TotalEarnings,
            MonthlyEarnings: aggregate.MonthlyEarnings,
            WeeklyEarnings: aggregate.WeeklyEarnings,
            AverageRating: aggregate.AverageRating,
            TotalReviews: aggregate.TotalReviews,
            RecentBookings: recentBookings,
            EarningsChart: earningsChart);

        await _cache.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5), cancellationToken);
        return new ApiResponse<VendorDashboardDto>(true, null, dashboard);
    }

    // Internal row for the aggregate query — not exposed outside
    private sealed class VendorAggregateRow
    {
        public int TotalParkingSpaces { get; set; }
        public int ActiveParkingSpaces { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalBookings { get; set; }
        public int ActiveBookings { get; set; }
        public int PendingBookings { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalEarnings { get; set; }
        public decimal MonthlyEarnings { get; set; }
        public decimal WeeklyEarnings { get; set; }
    }
}

public sealed class GetMemberDashboardHandler : IQueryHandler<GetMemberDashboardQuery, ApiResponse<MemberDashboardDto>>
{
    private readonly ISqlConnectionFactory _sql;
    private readonly ICacheService _cache;

    public GetMemberDashboardHandler(ISqlConnectionFactory sql, ICacheService cache)
    {
        _sql = sql;
        _cache = cache;
    }

    public async Task<ApiResponse<MemberDashboardDto>> HandleAsync(GetMemberDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:member:{query.MemberId}";
        var cached = await _cache.GetAsync<MemberDashboardDto>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<MemberDashboardDto>(true, null, cached);

        var now = DateTime.UtcNow;

        // ── Single aggregation query ──
        const string aggregateSql = """
            SELECT
                COUNT(b."Id")                                                                          AS "TotalBookings",
                COUNT(b."Id") FILTER (WHERE b."Status" IN (1, 3))                                     AS "ActiveBookings",
                COUNT(b."Id") FILTER (WHERE b."Status" = 4)                                           AS "CompletedBookings",
                COALESCE(SUM(b."TotalAmount") FILTER (WHERE p."Status" = 1), 0)                        AS "TotalSpent"
            FROM "Bookings" b
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE b."UserId" = @MemberId AND b."IsDeleted" = FALSE
            """;

        // ── Upcoming bookings (Confirmed or Pending, future start) ──
        const string upcomingSql = """
            SELECT
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", p."Status" AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE b."UserId" = @MemberId AND b."IsDeleted" = FALSE
                AND b."StartDateTime" > @Now AND b."Status" IN (0, 1)
            ORDER BY b."StartDateTime" ASC
            LIMIT 5
            """;

        // ── Recent completed bookings ──
        const string recentSql = """
            SELECT
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", p."Status" AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE b."UserId" = @MemberId AND b."IsDeleted" = FALSE AND b."Status" = 4
            ORDER BY b."CheckOutTime" DESC
            LIMIT 5
            """;

        using var connection = _sql.CreateConnection();

        var agg = await connection.QuerySingleAsync<MemberAggregateRow>(aggregateSql, new { query.MemberId });
        var upcomingBookings = (await connection.QueryAsync<BookingDto>(upcomingSql, new { query.MemberId, Now = now })).ToList();
        var recentBookings = (await connection.QueryAsync<BookingDto>(recentSql, new { query.MemberId })).ToList();

        var dashboard = new MemberDashboardDto(
            TotalBookings: agg.TotalBookings,
            ActiveBookings: agg.ActiveBookings,
            CompletedBookings: agg.CompletedBookings,
            TotalSpent: agg.TotalSpent,
            UpcomingBookings: upcomingBookings,
            RecentBookings: recentBookings);

        await _cache.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5), cancellationToken);
        return new ApiResponse<MemberDashboardDto>(true, null, dashboard);
    }

    private sealed class MemberAggregateRow
    {
        public int TotalBookings { get; set; }
        public int ActiveBookings { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalSpent { get; set; }
    }
}
