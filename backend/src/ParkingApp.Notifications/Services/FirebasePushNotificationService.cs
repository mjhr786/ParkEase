using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Notifications.Services;

/// <summary>
/// Firebase FCM push notification service using the Firebase Admin SDK.
/// Replaces the mock implementation in production.
/// </summary>
public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<FirebasePushNotificationService> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<PushResult> SendToDeviceAsync(
        string deviceToken,
        PushNotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = BuildMessage(deviceToken, payload);
            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);

            _logger.LogInformation(
                "FCM push sent to device {Token}: {Title} | messageId={MessageId}",
                MaskToken(deviceToken), payload.Title, messageId);

            return new PushResult(Success: true, MessageId: messageId, SuccessCount: 1, FailureCount: 0);
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex,
                "FCM error sending to device {Token}: {ErrorCode}",
                MaskToken(deviceToken), ex.MessagingErrorCode);

            return new PushResult(
                Success: false,
                ErrorMessage: ex.Message,
                SuccessCount: 0,
                FailureCount: 1);
        }
    }

    public async Task<PushResult> SendToUserAsync(
        Guid userId,
        PushNotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        var tokens = (await _deviceTokenRepository.GetFcmTokensByUserIdAsync(userId, cancellationToken)).ToList();

        if (tokens.Count == 0)
        {
            _logger.LogInformation("No FCM tokens registered for user {UserId} — push skipped", userId);
            return new PushResult(Success: true, SuccessCount: 0, FailureCount: 0);
        }

        if (tokens.Count == 1)
        {
            return await SendToDeviceAsync(tokens[0], payload, cancellationToken);
        }

        // Multicast for multiple devices
        try
        {
            var multicast = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new Notification
                {
                    Title = payload.Title,
                    Body = payload.Body,
                    ImageUrl = payload.ImageUrl
                },
                Data = payload.Data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = MapAndroidPriority(payload.Priority)
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicast, cancellationToken);

            _logger.LogInformation(
                "FCM multicast to user {UserId}: {SuccessCount} success, {FailureCount} failure",
                userId, response.SuccessCount, response.FailureCount);

            return new PushResult(
                Success: response.SuccessCount > 0,
                SuccessCount: response.SuccessCount,
                FailureCount: response.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM multicast failed for user {UserId}", userId);
            return new PushResult(Success: false, ErrorMessage: ex.Message, SuccessCount: 0, FailureCount: tokens.Count);
        }
    }

    public async Task<PushResult> SendToTopicAsync(
        string topic,
        PushNotificationPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message
            {
                Topic = topic,
                Notification = new Notification
                {
                    Title = payload.Title,
                    Body = payload.Body,
                    ImageUrl = payload.ImageUrl
                },
                Data = payload.Data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = MapAndroidPriority(payload.Priority)
                }
            };

            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);

            _logger.LogInformation(
                "FCM topic push to {Topic}: {Title} | messageId={MessageId}",
                topic, payload.Title, messageId);

            return new PushResult(Success: true, MessageId: messageId, SuccessCount: 1, FailureCount: 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM topic push failed for topic {Topic}", topic);
            return new PushResult(Success: false, ErrorMessage: ex.Message, SuccessCount: 0, FailureCount: 1);
        }
    }

    public async Task<bool> SubscribeToTopicAsync(
        string deviceToken,
        string topic,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await FirebaseMessaging.DefaultInstance
                .SubscribeToTopicAsync(new[] { deviceToken }, topic);
            _logger.LogDebug("Subscribed {Token} to topic {Topic}: {SuccessCount} success",
                MaskToken(deviceToken), topic, response.SuccessCount);
            return response.SuccessCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to subscribe {Token} to topic {Topic}", MaskToken(deviceToken), topic);
            return false;
        }
    }

    public async Task<bool> UnsubscribeFromTopicAsync(
        string deviceToken,
        string topic,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await FirebaseMessaging.DefaultInstance
                .UnsubscribeFromTopicAsync(new[] { deviceToken }, topic);
            _logger.LogDebug("Unsubscribed {Token} from topic {Topic}: {SuccessCount} success",
                MaskToken(deviceToken), topic, response.SuccessCount);
            return response.SuccessCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unsubscribe {Token} from topic {Topic}", MaskToken(deviceToken), topic);
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Message BuildMessage(string deviceToken, PushNotificationPayload payload) => new()
    {
        Token = deviceToken,
        Notification = new Notification
        {
            Title = payload.Title,
            Body = payload.Body,
            ImageUrl = payload.ImageUrl
        },
        Data = payload.Data ?? new Dictionary<string, string>(),
        Android = new AndroidConfig
        {
            Priority = MapAndroidPriority(payload.Priority)
        }
    };

    private static Priority MapAndroidPriority(PushPriority priority) => priority switch
    {
        PushPriority.High => Priority.High,
        _ => Priority.Normal
    };

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 8) return "****";
        return token[..4] + "..." + token[^4..];
    }
}
