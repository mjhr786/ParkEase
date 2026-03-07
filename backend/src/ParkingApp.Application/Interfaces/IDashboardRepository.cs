using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

public interface IDashboardRepository
{
    Task<VendorAggregateRow> GetVendorAggregatesAsync(Guid vendorId, DateTime startOfMonth, DateTime startOfWeek, CancellationToken ct = default);
    Task<List<DashboardChartDataDto>> GetChartDataAsync(Guid vendorId, CancellationToken ct = default);
    Task<List<BookingDto>> GetRecentVendorBookingsAsync(Guid vendorId, CancellationToken ct = default);
    
    Task<MemberAggregateRow> GetMemberAggregatesAsync(Guid memberId, CancellationToken ct = default);
    Task<List<BookingDto>> GetUpcomingMemberBookingsAsync(Guid memberId, DateTime now, CancellationToken ct = default);
    Task<List<BookingDto>> GetRecentMemberBookingsAsync(Guid memberId, CancellationToken ct = default);
}
