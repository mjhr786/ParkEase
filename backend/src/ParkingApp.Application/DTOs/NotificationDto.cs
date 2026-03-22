using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

/// <summary>
/// DTO representing a persisted notification in the user's notification history.
/// Distinct from the real-time NotificationDto used for SignalR.
/// </summary>
public record NotificationHistoryDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string? Data,
    bool IsRead,
    DateTime CreatedAt
);

public record NotificationListDto(
    PagedNotificationsDto Notifications,
    int UnreadCount
);

public record PagedNotificationsDto(
    IReadOnlyList<NotificationHistoryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
);
