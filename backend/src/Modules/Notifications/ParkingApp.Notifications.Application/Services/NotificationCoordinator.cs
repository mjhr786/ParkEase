using Microsoft.Extensions.Logging;
using ParkingApp.Notifications.Contracts;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Identity.Contracts;
using InboxType = ParkingApp.Messaging.Contracts.Enums.NotificationType;
using InboxPriority = ParkingApp.Messaging.Contracts.Enums.NotificationPriority;

namespace ParkingApp.Notifications.Application.Services;

/// <summary>
/// Notification coordinator that routes notifications through configured channels.
/// Orchestrates delivery via In-App (SignalR), SMS, and Push notifications.
/// </summary>
internal class NotificationCoordinator : INotificationCoordinator
{
    private readonly INotificationService _inAppNotificationService;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly INotificationInbox _inbox;
    private readonly IUserLookup _users;
    private readonly ILogger<NotificationCoordinator> _logger;

    public NotificationCoordinator(
        INotificationService inAppNotificationService,
        ISmsNotificationService smsNotificationService,
        IPushNotificationService pushNotificationService,
        INotificationInbox inbox,
        IUserLookup users,
        ILogger<NotificationCoordinator> logger)
    {
        _inAppNotificationService = inAppNotificationService;
        _smsNotificationService = smsNotificationService;
        _pushNotificationService = pushNotificationService;
        _inbox = inbox;
        _users = users;
        _logger = logger;
    }

    public async Task SendAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending notification to user {UserId}: Type={Type}, Channels={Channels}",
            userId, request.Type, request.Channels);

        var type = Enum.TryParse<InboxType>(request.Type, true, out var parsedType)
            ? parsedType
            : InboxType.SystemAlert;
        var priority = (InboxPriority)(int)request.Priority;
        var data = request.Data != null
            ? System.Text.Json.JsonSerializer.Serialize(request.Data)
            : null;

        await _inbox.AddAsync(
            userId,
            type,
            priority,
            request.Title,
            request.Message,
            data,
            cancellationToken);

        try
        {
            if (request.Channels.HasFlag(NotificationChannels.InApp))
                await SendInAppAsync(userId, request, data, cancellationToken);

            if (request.Channels.HasFlag(NotificationChannels.Push))
                await SendPushAsync(userId, request, cancellationToken);

            if (request.Channels.HasFlag(NotificationChannels.Sms))
                await SendSmsAsync(userId, request, cancellationToken);

            _logger.LogDebug("All notification channels completed for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications to user {UserId}", userId);
        }
    }

    public async Task SendBulkAsync(IEnumerable<Guid> userIds, NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var userIdList = userIds.ToList();
        _logger.LogInformation(
            "Sending bulk notification to {UserCount} users: Type={Type}",
            userIdList.Count, request.Type);

        foreach (var userId in userIdList)
            await SendAsync(userId, request, cancellationToken);

        _logger.LogInformation("Bulk notification completed for {UserCount} users", userIdList.Count);
    }

    private async Task SendInAppAsync(Guid userId, NotificationRequest request, string? dataJson, CancellationToken cancellationToken)
    {
        try
        {
            object? dataPayload = dataJson;
            if (!string.IsNullOrWhiteSpace(dataJson))
            {
                try
                {
                    dataPayload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson);
                }
                catch
                {
                    // keep raw string
                }
            }

            await _inAppNotificationService.NotifyUserAsync(
                userId,
                new NotificationDto(request.Type, request.Title, request.Message, dataPayload),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send in-app notification to user {UserId}", userId);
        }
    }

    private async Task SendPushAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _pushNotificationService.SendToUserAsync(
                userId,
                new PushNotificationPayload(
                    request.Title,
                    request.Message,
                    ImageUrl: null,
                    Data: request.Data,
                    Priority: MapPriority(request.Priority)),
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Push notification failed for user {UserId}: {ErrorMessage}",
                    userId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification to user {UserId}", userId);
        }
    }

    private async Task SendSmsAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Priority < NotificationPriority.High)
            {
                _logger.LogDebug("SMS skipped for user {UserId}: priority too low", userId);
                return;
            }

            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user == null || string.IsNullOrEmpty(user.PhoneNumber))
            {
                _logger.LogDebug("SMS skipped for user {UserId}: no phone number", userId);
                return;
            }

            var message = $"{request.Title}: {request.Message}";
            var result = await _smsNotificationService.SendAsync(user.PhoneNumber, message, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "SMS notification failed for user {UserId}: {ErrorMessage}",
                    userId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SMS notification to user {UserId}", userId);
        }
    }

    private static PushPriority MapPriority(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Low => PushPriority.Low,
        NotificationPriority.Normal => PushPriority.Normal,
        NotificationPriority.High => PushPriority.High,
        NotificationPriority.Critical => PushPriority.High,
        _ => PushPriority.Normal
    };
}
