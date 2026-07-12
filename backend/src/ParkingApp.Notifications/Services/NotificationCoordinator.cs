using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Notifications.Services;

/// <summary>
/// Notification coordinator that routes notifications through configured channels.
/// Orchestrates delivery via In-App (SignalR), SMS, and Push notifications.
/// </summary>
public class NotificationCoordinator : INotificationCoordinator
{
    private readonly INotificationService _inAppNotificationService;
    private readonly ISmsNotificationService _smsNotificationService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationCoordinator> _logger;
    
    public NotificationCoordinator(
        INotificationService inAppNotificationService,
        ISmsNotificationService smsNotificationService,
        IPushNotificationService pushNotificationService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationCoordinator> logger)
    {
        _inAppNotificationService = inAppNotificationService;
        _smsNotificationService = smsNotificationService;
        _pushNotificationService = pushNotificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    
    public async Task SendAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending notification to user {UserId}: Type={Type}, Channels={Channels}",
            userId, request.Type, request.Channels);
        // Save to DB first
        var notificationEntity = new ParkingApp.Domain.Messaging.Notification
        {
            UserId = userId,
            Type = Enum.TryParse<ParkingApp.Domain.Enums.NotificationType>(request.Type, true, out var parsedType)
                ? parsedType
                : ParkingApp.Domain.Enums.NotificationType.SystemAlert,
            Priority = (ParkingApp.Domain.Enums.NotificationPriority)(int)request.Priority,
            Title = request.Title,
            Message = request.Message,
            Data = request.Data != null
                ? System.Text.Json.JsonSerializer.Serialize(request.Data)
                : null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        
        await _unitOfWork.Notifications.AddAsync(notificationEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Deliver channels sequentially. Push and SMS both query the scoped ApplicationDbContext
        // (device tokens / user phone); running them in parallel via Task.WhenAll causes
        // "A second operation was started on this context instance before a previous operation completed".
        try
        {
            if (request.Channels.HasFlag(NotificationChannels.InApp))
            {
                await SendInAppAsync(userId, notificationEntity, cancellationToken);
            }

            if (request.Channels.HasFlag(NotificationChannels.Push))
            {
                await SendPushAsync(userId, request, cancellationToken);
            }

            if (request.Channels.HasFlag(NotificationChannels.Sms))
            {
                await SendSmsAsync(userId, request, cancellationToken);
            }

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

        // Sequential: each SendAsync uses the same scoped IUnitOfWork / DbContext.
        foreach (var userId in userIdList)
        {
            await SendAsync(userId, request, cancellationToken);
        }
        
        _logger.LogInformation("Bulk notification completed for {UserCount} users", userIdList.Count);
    }
    
    private async Task SendInAppAsync(Guid userId, ParkingApp.Domain.Messaging.Notification entity, CancellationToken cancellationToken)
    {
        try
        {
            // Use the real-time NotificationDto (from INotificationService) for SignalR dispatch.
            // The persisted entity is already saved; this DTO carries the real-time payload.
            var notificationDto = new ParkingApp.Application.Interfaces.NotificationDto(
                Type: entity.Type.ToString(),
                Title: entity.Title,
                Message: entity.Message,
                Data: (object?)entity.Data
            );
            
            await _inAppNotificationService.NotifyUserAsync(userId, notificationDto, cancellationToken);
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
            var payload = new PushNotificationPayload(
                Title: request.Title,
                Body: request.Message,
                Data: request.Data,
                Priority: MapPriority(request.Priority)
            );
            
            var result = await _pushNotificationService.SendToUserAsync(userId, payload, cancellationToken);
            
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
            // Only send SMS for high priority or critical notifications (check before DB access)
            if (request.Priority < NotificationPriority.High)
            {
                _logger.LogDebug("SMS skipped for user {UserId}: priority too low", userId);
                return;
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
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
