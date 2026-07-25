namespace ParkingApp.Identity.Contracts;

/// <summary>
/// Identity module contract: other modules request user summaries without referencing User.
/// </summary>
public interface IUserLookup
{
    Task<UserSummary?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active users for the given ids. Missing or inactive users are omitted.
    /// </summary>
    Task<IReadOnlyList<UserSummary>> GetActiveByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
