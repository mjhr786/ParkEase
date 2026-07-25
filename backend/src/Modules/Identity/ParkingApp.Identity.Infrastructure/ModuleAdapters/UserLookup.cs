using ParkingApp.Identity.Contracts;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Infrastructure.ModuleAdapters;

/// <summary>
/// Identity adapter: maps repository User aggregate to contract summary.
/// </summary>
internal sealed class UserLookup : IUserLookup
{
    private readonly IUserRepository _users;

    public UserLookup(IUserRepository users) => _users = users;

    public async Task<UserSummary?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return Map(user);
    }

    public async Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(email, cancellationToken);
        return Map(user);
    }

    public async Task<IReadOnlyList<UserSummary>> GetActiveByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return Array.Empty<UserSummary>();

        var idSet = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idSet.Count == 0)
            return Array.Empty<UserSummary>();

        var users = await _users.FindAsync(
            user => idSet.Contains(user.Id) && user.IsActive,
            cancellationToken);

        return users.Select(Map).Where(s => s is not null).Cast<UserSummary>().ToList();
    }

    private static UserSummary? Map(ParkingApp.Identity.Domain.Entities.User? user)
    {
        if (user is null)
            return null;

        return new UserSummary(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber,
            user.IsActive,
            user.Role == UserRole.Admin);
    }
}
