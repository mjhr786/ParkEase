namespace ParkingApp.Corporate.Contracts;

/// <summary>
/// Cross-module read of active company memberships (Contracts only — no Domain types).
/// Used by Identity corporate login / channel switch (PR3).
/// </summary>
public interface ICompanyMembershipLookup
{
    Task<IReadOnlyList<CompanyMembershipSummary>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CompanyMembershipSummary?> GetActiveMembershipAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default);
}

/// <param name="Role">Admin or Employee (company role, not platform role).</param>
public sealed record CompanyMembershipSummary(
    Guid CompanyId,
    string CompanyName,
    string Role);
