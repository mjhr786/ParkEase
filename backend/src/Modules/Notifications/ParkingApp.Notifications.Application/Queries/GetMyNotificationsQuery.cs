using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Messaging.Contracts;

namespace ParkingApp.Notifications.Application.Queries.Notifications;

public sealed record GetMyNotificationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IQuery<ApiResponse<NotificationListDto>>;

internal sealed class GetMyNotificationsQueryHandler : IQueryHandler<GetMyNotificationsQuery, ApiResponse<NotificationListDto>>
{
    private readonly INotificationInbox _inbox;

    public GetMyNotificationsQueryHandler(INotificationInbox inbox)
    {
        _inbox = inbox;
    }

    public async Task<ApiResponse<NotificationListDto>> HandleAsync(GetMyNotificationsQuery query, CancellationToken cancellationToken = default)
    {
        var notifications = await _inbox.GetPagedAsync(query.UserId, query.Page, query.PageSize, cancellationToken);
        var totalCount = await _inbox.GetTotalCountAsync(query.UserId, cancellationToken);
        var unreadCount = await _inbox.GetUnreadCountAsync(query.UserId, cancellationToken);

        var dtos = notifications.Select(n => new NotificationHistoryDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.Data,
                n.IsRead,
                n.CreatedAt)).ToList();

        var totalPages = query.PageSize > 0 ? (int)Math.Ceiling(totalCount / (double)query.PageSize) : 0;
        var pagedResult = new PagedNotificationsDto(dtos, totalCount, query.Page, query.PageSize, totalPages, query.Page > 1, query.Page < totalPages);

        return new ApiResponse<NotificationListDto>(true, "Notifications retrieved", new NotificationListDto(pagedResult, unreadCount));
    }
}
