namespace ParkingApp.Corporate.Contracts;

/// <summary>
/// Loads and unprotects per-company OIDC client secrets for token exchange.
/// Identity SSO callback depends on this contract only (no Corporate.Domain).
/// </summary>
public interface ISsoClientSecretAccessor
{
    /// <summary>
    /// Returns (true, secret) on success; (false, null) when missing or unreadable.
    /// </summary>
    Task<(bool Ok, string? Secret)> TryGetClientSecretAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);
}
