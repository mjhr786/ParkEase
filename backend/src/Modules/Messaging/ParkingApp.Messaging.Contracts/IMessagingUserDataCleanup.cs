namespace ParkingApp.Messaging.Contracts;

/// <summary>
/// Cross-module port: hard-delete messaging data owned by a user.
/// Stages changes only — caller owns transaction and <c>SaveChanges</c>.
/// </summary>
public interface IMessagingUserDataCleanup
{
    Task StageDeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
