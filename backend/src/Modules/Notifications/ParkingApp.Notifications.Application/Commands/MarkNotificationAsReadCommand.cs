using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Messaging.Contracts;

namespace ParkingApp.Notifications.Application.Commands.Notifications;

public sealed record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId) : ICommand<ApiResponse<bool>>;

internal sealed class MarkNotificationAsReadCommandHandler : ICommandHandler<MarkNotificationAsReadCommand, ApiResponse<bool>>
{
    private readonly INotificationInbox _inbox;

    public MarkNotificationAsReadCommandHandler(INotificationInbox inbox) => _inbox = inbox;

    public async Task<ApiResponse<bool>> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken = default)
    {
        var ok = await _inbox.MarkAsReadAsync(command.NotificationId, command.UserId, cancellationToken);
        return ok
            ? new ApiResponse<bool>(true, "Notification marked as read", true)
            : new ApiResponse<bool>(false, "Notification not found", false);
    }
}

public sealed record MarkAllNotificationsAsReadCommand(Guid UserId) : ICommand<ApiResponse<bool>>;

internal sealed class MarkAllNotificationsAsReadCommandHandler : ICommandHandler<MarkAllNotificationsAsReadCommand, ApiResponse<bool>>
{
    private readonly INotificationInbox _inbox;

    public MarkAllNotificationsAsReadCommandHandler(INotificationInbox inbox) => _inbox = inbox;

    public async Task<ApiResponse<bool>> HandleAsync(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken = default)
    {
        await _inbox.MarkAllAsReadAsync(command.UserId, cancellationToken);
        return new ApiResponse<bool>(true, "All notifications marked as read", true);
    }
}

public sealed record DeleteNotificationCommand(Guid NotificationId, Guid UserId) : ICommand<ApiResponse<bool>>;

internal sealed class DeleteNotificationCommandHandler : ICommandHandler<DeleteNotificationCommand, ApiResponse<bool>>
{
    private readonly INotificationInbox _inbox;

    public DeleteNotificationCommandHandler(INotificationInbox inbox) => _inbox = inbox;

    public async Task<ApiResponse<bool>> HandleAsync(DeleteNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var ok = await _inbox.DeleteAsync(command.NotificationId, command.UserId, cancellationToken);
        return ok
            ? new ApiResponse<bool>(true, "Notification deleted successfully", true)
            : new ApiResponse<bool>(false, "Notification not found", false);
    }
}

public sealed record ClearAllNotificationsCommand(Guid UserId) : ICommand<ApiResponse<bool>>;

internal sealed class ClearAllNotificationsCommandHandler : ICommandHandler<ClearAllNotificationsCommand, ApiResponse<bool>>
{
    private readonly INotificationInbox _inbox;

    public ClearAllNotificationsCommandHandler(INotificationInbox inbox) => _inbox = inbox;

    public async Task<ApiResponse<bool>> HandleAsync(ClearAllNotificationsCommand command, CancellationToken cancellationToken = default)
    {
        await _inbox.DeleteAllAsync(command.UserId, cancellationToken);
        return new ApiResponse<bool>(true, "All notifications cleared successfully", true);
    }
}
