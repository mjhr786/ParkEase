namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Cross-module port: hard-delete marketplace data owned by a user.
/// Stages changes only — caller owns transaction and <c>SaveChanges</c>.
/// </summary>
public interface IMarketplaceUserDataCleanup
{
    Task StageDeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
