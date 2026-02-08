using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Notifications.Services;

/// <summary>
/// Mock Push notification service for development and testing.
/// Replace with actual provider (Firebase FCM, APNs, OneSignal, etc.) in production.
/// </summary>
public class MockPushNotificationService : IPushNotificationService
{
    private readonly ILogger<MockPushNotificationService> _logger;
    private readonly Dictionary<string, HashSet<string>> _topicSubscriptions = new();
    private readonly Dictionary<Guid, HashSet<string>> _userDevices = new();
    
    public MockPushNotificationService(ILogger<MockPushNotificationService> logger)
    {
        _logger = logger;
    }
    
    public async Task<PushResult> SendToDeviceAsync(string deviceToken, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        
        var messageId = $"PUSH-{Guid.NewGuid():N}";
        
        _logger.LogInformation(
            "Push notification sent to device {DeviceToken}: {Title} (MessageId: {MessageId})",
            MaskDeviceToken(deviceToken),
            payload.Title,
            messageId);
        
        return new PushResult(
            Success: true,
            MessageId: messageId,
            SuccessCount: 1,
            FailureCount: 0
        );
    }
    
    public async Task<PushResult> SendToUserAsync(Guid userId, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        var messageId = $"PUSH-{Guid.NewGuid():N}";
        
        // In a real implementation, lookup user's registered devices from database
        if (_userDevices.TryGetValue(userId, out var devices) && devices.Count > 0)
        {
            var successCount = 0;
            foreach (var device in devices)
            {
                var result = await SendToDeviceAsync(device, payload, cancellationToken);
                if (result.Success) successCount++;
            }
            
            _logger.LogInformation(
                "Push notification sent to user {UserId} ({DeviceCount} devices): {Title}",
                userId, devices.Count, payload.Title);
            
            return new PushResult(
                Success: successCount > 0,
                MessageId: messageId,
                SuccessCount: successCount,
                FailureCount: devices.Count - successCount
            );
        }
        
        // Mock: simulate successful send even without registered devices
        _logger.LogInformation(
            "Push notification queued for user {UserId}: {Title} (no devices registered)",
            userId, payload.Title);
        
        return new PushResult(
            Success: true,
            MessageId: messageId,
            SuccessCount: 1,
            FailureCount: 0
        );
    }
    
    public async Task<PushResult> SendToTopicAsync(string topic, PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        
        var messageId = $"PUSH-TOPIC-{Guid.NewGuid():N}";
        var subscriberCount = _topicSubscriptions.TryGetValue(topic, out var subscribers) ? subscribers.Count : 0;
        
        _logger.LogInformation(
            "Push notification sent to topic {Topic} ({SubscriberCount} subscribers): {Title}",
            topic, subscriberCount, payload.Title);
        
        return new PushResult(
            Success: true,
            MessageId: messageId,
            SuccessCount: subscriberCount > 0 ? subscriberCount : 1,
            FailureCount: 0
        );
    }
    
    public Task<bool> SubscribeToTopicAsync(string deviceToken, string topic, CancellationToken cancellationToken = default)
    {
        if (!_topicSubscriptions.TryGetValue(topic, out var subscribers))
        {
            subscribers = new HashSet<string>();
            _topicSubscriptions[topic] = subscribers;
        }
        
        var added = subscribers.Add(deviceToken);
        
        if (added)
        {
            _logger.LogDebug("Device {DeviceToken} subscribed to topic {Topic}", 
                MaskDeviceToken(deviceToken), topic);
        }
        
        return Task.FromResult(added);
    }
    
    public Task<bool> UnsubscribeFromTopicAsync(string deviceToken, string topic, CancellationToken cancellationToken = default)
    {
        if (_topicSubscriptions.TryGetValue(topic, out var subscribers))
        {
            var removed = subscribers.Remove(deviceToken);
            
            if (removed)
            {
                _logger.LogDebug("Device {DeviceToken} unsubscribed from topic {Topic}", 
                    MaskDeviceToken(deviceToken), topic);
            }
            
            return Task.FromResult(removed);
        }
        
        return Task.FromResult(false);
    }
    
    /// <summary>
    /// Register a device for a user (called during device registration).
    /// </summary>
    public void RegisterUserDevice(Guid userId, string deviceToken)
    {
        if (!_userDevices.TryGetValue(userId, out var devices))
        {
            devices = new HashSet<string>();
            _userDevices[userId] = devices;
        }
        
        devices.Add(deviceToken);
        _logger.LogDebug("Device registered for user {UserId}", userId);
    }
    
    private static string MaskDeviceToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 8)
            return "****";
        
        return token[..4] + "..." + token[^4..];
    }
}
