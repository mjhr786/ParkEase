namespace ParkingApp.Corporate.Contracts;

/// <summary>
/// Company-admin operations on Corporate SSO identity links (Identity-owned entity).
/// </summary>
public interface ICorporateSsoLinkAdmin
{
    /// <summary>
    /// Soft-disables a link for the company. Returns false when not found / wrong company.
    /// </summary>
    Task<bool> DisableLinkAsync(
        Guid companyId,
        Guid linkId,
        CancellationToken cancellationToken = default);
}
