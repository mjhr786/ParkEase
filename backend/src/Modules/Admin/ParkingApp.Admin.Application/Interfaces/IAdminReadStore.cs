using ParkingApp.Admin.Application.DTOs;

namespace ParkingApp.Admin.Application.Interfaces;

/// <summary>
/// Cross-table ops reads for the platform admin panel (Dapper against shared DB).
/// </summary>
public interface IAdminReadStore
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<AdminAuditLogPageDto> GetAuditLogsAsync(
        string? action,
        string? resourceType,
        Guid? actorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
