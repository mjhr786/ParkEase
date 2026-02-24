using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Notifications;

// --- Mark Single Notification as Read ---

public sealed record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId) : ICommand<ApiResponse<bool>>;

public sealed class MarkNotificationAsReadCommandHandler : ICommandHandler<MarkNotificationAsReadCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(command.NotificationId, cancellationToken);

        if (notification == null || notification.UserId != command.UserId)
            return new ApiResponse<bool>(false, "Notification not found", false);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ApiResponse<bool>(true, "Notification marked as read", true);
    }
}

// --- Mark All Notifications as Read ---

public sealed record MarkAllNotificationsAsReadCommand(Guid UserId) : ICommand<ApiResponse<bool>>;

public sealed class MarkAllNotificationsAsReadCommandHandler : ICommandHandler<MarkAllNotificationsAsReadCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public MarkAllNotificationsAsReadCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Notifications.MarkAllAsReadAsync(command.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "All notifications marked as read", true);
    }
}

// --- Delete Notification ---

public sealed record DeleteNotificationCommand(Guid NotificationId, Guid UserId) : ICommand<ApiResponse<bool>>;

public sealed class DeleteNotificationCommandHandler : ICommandHandler<DeleteNotificationCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteNotificationCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(command.NotificationId, cancellationToken);

        if (notification == null || notification.UserId != command.UserId)
            return new ApiResponse<bool>(false, "Notification not found", false);

        _unitOfWork.Notifications.Remove(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool>(true, "Notification deleted successfully", true);
    }
}
