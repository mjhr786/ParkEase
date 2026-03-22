using ParkingApp.Application.Interfaces;
using ParkingApp.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Queries.Dashboard;

public record GetVendorDashboardQuery(Guid VendorId) : IQuery<ApiResponse<VendorDashboardDto>>;
public record GetMemberDashboardQuery(Guid MemberId) : IQuery<ApiResponse<MemberDashboardDto>>;

public sealed class VendorAggregateRow
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

public sealed class MemberAggregateRow
{
    public int TotalBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedBookings { get; set; }
    public decimal TotalSpent { get; set; }
}

public class GetVendorDashboardHandler : IQueryHandler<GetVendorDashboardQuery, ApiResponse<VendorDashboardDto>>
{
    private readonly IDashboardRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<GetVendorDashboardHandler> _logger;

    public GetVendorDashboardHandler(IDashboardRepository repository, ICacheService cache, ILogger<GetVendorDashboardHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<VendorDashboardDto>> HandleAsync(GetVendorDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:vendor:{query.VendorId}";
        var cachedData = await _cache.GetAsync<VendorDashboardDto>(cacheKey, cancellationToken);
        if (cachedData != null) return new ApiResponse<VendorDashboardDto>(true, null, cachedData);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);

        var aggregate = await _repository.GetVendorAggregatesAsync(query.VendorId, startOfMonth, startOfWeek, cancellationToken);
        var chartData = await _repository.GetChartDataAsync(query.VendorId, cancellationToken);
        var recentBookings = await _repository.GetRecentVendorBookingsAsync(query.VendorId, cancellationToken);

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
            ChartData: chartData);

        await _cache.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5), cancellationToken);
        return new ApiResponse<VendorDashboardDto>(true, null, dashboard);
    }
}

public class GetMemberDashboardHandler : IQueryHandler<GetMemberDashboardQuery, ApiResponse<MemberDashboardDto>>
{
    private readonly IDashboardRepository _repository;
    private readonly ICacheService _cache;

    public GetMemberDashboardHandler(IDashboardRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<ApiResponse<MemberDashboardDto>> HandleAsync(GetMemberDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:member:{query.MemberId}";
        var cachedData = await _cache.GetAsync<MemberDashboardDto>(cacheKey, cancellationToken);
        if (cachedData != null) return new ApiResponse<MemberDashboardDto>(true, null, cachedData);

        var now = DateTime.UtcNow;

        var agg = await _repository.GetMemberAggregatesAsync(query.MemberId, cancellationToken);
        var upcomingBookings = await _repository.GetUpcomingMemberBookingsAsync(query.MemberId, now, cancellationToken);
        var recentBookings = await _repository.GetRecentMemberBookingsAsync(query.MemberId, cancellationToken);

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
}
