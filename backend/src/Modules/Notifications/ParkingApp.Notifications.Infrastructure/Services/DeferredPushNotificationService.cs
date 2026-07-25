using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingApp.Notifications.Contracts;

namespace ParkingApp.Notifications.Infrastructure.Services;

/// <summary>
/// Fire-and-forget push using a fresh DI scope so scoped FCM/device-token services are safe after the HTTP request ends.
/// </summary>
internal sealed class DeferredPushNotificationService : IDeferredPushNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeferredPushNotificationService> _logger;

    public DeferredPushNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeferredPushNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void ScheduleSendToUser(Guid userId, PushNotificationPayload payload)
    {
        // Capture values; do not capture request-scoped services.
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await push.SendToUserAsync(userId, payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deferred push failed for user {UserId}", userId);
            }
        });
    }
}
