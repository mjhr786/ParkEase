using ParkingApp.Admin.Application.DTOs;
using ParkingApp.Admin.Application.Interfaces;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Admin.Application.Queries;

public sealed record GetAdminDashboardQuery : IQuery<ApiResponse<AdminDashboardDto>>;

internal sealed class GetAdminDashboardHandler : IQueryHandler<GetAdminDashboardQuery, ApiResponse<AdminDashboardDto>>
{
    private readonly IAdminReadStore _readStore;

    public GetAdminDashboardHandler(IAdminReadStore readStore) => _readStore = readStore;

    public async Task<ApiResponse<AdminDashboardDto>> HandleAsync(
        GetAdminDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var data = await _readStore.GetDashboardAsync(cancellationToken);
        return new ApiResponse<AdminDashboardDto>(true, null, data);
    }
}
