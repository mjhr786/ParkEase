namespace ParkingApp.Corporate.Contracts;

/// <summary>
/// Server-side invite acceptance for Corporate SSO (no Corporate.Domain leak into Identity).
/// MVP always attempts pending invite accept (KD-CS-29) — not gated on AutoAccept flag.
/// </summary>
public interface ICorporateSsoMembershipProvisioner
{
    /// <summary>
    /// If a pending non-expired invite exists for normalized email, accept it server-side
    /// (role from invitation). Returns accepted role or failure reason.
    /// </summary>
    Task<SsoInviteAcceptResult> TryAcceptPendingInviteAsync(
        Guid companyId,
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken = default);
}

/// <param name="CompanyRole">"Admin" | "Employee" when Accepted.</param>
/// <param name="ErrorCode">no_pending_invite | invite_expired | already_member | company_inactive | ...</param>
public sealed record SsoInviteAcceptResult(
    bool Accepted,
    string? CompanyRole,
    string? ErrorCode);
