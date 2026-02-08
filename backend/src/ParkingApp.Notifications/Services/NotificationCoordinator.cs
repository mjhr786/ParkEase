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
        
        var tasks = new List<Task>();
        
        // In-App notification (SignalR)
        if (request.Channels.HasFlag(NotificationChannels.InApp))
        {
            tasks.Add(SendInAppAsync(userId, request, cancellationToken));
        }
        
        // Push notification
        if (request.Channels.HasFlag(NotificationChannels.Push))
        {
            tasks.Add(SendPushAsync(userId, request, cancellationToken));
        }
        
        // SMS notification
        if (request.Channels.HasFlag(NotificationChannels.Sms))
        {
            tasks.Add(SendSmsAsync(userId, request, cancellationToken));
        }
        
        // Wait for all channels to complete
        try
        {
            await Task.WhenAll(tasks);
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
        
        var tasks = userIdList.Select(userId => SendAsync(userId, request, cancellationToken));
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Bulk notification completed for {UserCount} users", userIdList.Count);
    }
    
    private async Task SendInAppAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var notification = new NotificationDto(
                Type: request.Type,
                Title: request.Title,
                Message: request.Message,
                Data: request.Data
            );
            
            await _inAppNotificationService.NotifyUserAsync(userId, notification, cancellationToken);
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
            // Lookup user's phone number
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user == null || string.IsNullOrEmpty(user.PhoneNumber))
            {
                _logger.LogDebug("SMS skipped for user {UserId}: no phone number", userId);
                return;
            }
            
            // Only send SMS for high priority or critical notifications
            if (request.Priority < NotificationPriority.High)
            {
                _logger.LogDebug("SMS skipped for user {UserId}: priority too low", userId);
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
