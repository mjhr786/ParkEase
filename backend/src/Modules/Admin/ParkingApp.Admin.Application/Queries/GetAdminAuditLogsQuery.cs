using ParkingApp.Admin.Application.DTOs;
using ParkingApp.Admin.Application.Interfaces;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Admin.Application.Queries;

public sealed record GetAdminAuditLogsQuery(
    string? Action,
    string? ResourceType,
    Guid? ActorUserId,
    int Page = 1,
    int PageSize = 25) : IQuery<ApiResponse<AdminAuditLogPageDto>>;

internal sealed class GetAdminAuditLogsHandler : IQueryHandler<GetAdminAuditLogsQuery, ApiResponse<AdminAuditLogPageDto>>
{
    private readonly IAdminReadStore _readStore;

    public GetAdminAuditLogsHandler(IAdminReadStore readStore) => _readStore = readStore;

    public async Task<ApiResponse<AdminAuditLogPageDto>> HandleAsync(
        GetAdminAuditLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, 100);
        var data = await _readStore.GetAuditLogsAsync(
            query.Action,
            query.ResourceType,
            query.ActorUserId,
            page,
            pageSize,
            cancellationToken);
        return new ApiResponse<AdminAuditLogPageDto>(true, null, data);
    }
}
