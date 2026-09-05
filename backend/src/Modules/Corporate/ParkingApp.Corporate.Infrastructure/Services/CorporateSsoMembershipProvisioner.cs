using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>
/// Server-side invite accept for Corporate SSO (KD-CS-29). Always attempts pending invite;
/// never calls public HTTP accept API.
/// </summary>
public sealed class CorporateSsoMembershipProvisioner : ICorporateSsoMembershipProvisioner
{
    private readonly ICorporateDbContext _db;
    private readonly ILogger<CorporateSsoMembershipProvisioner> _logger;

    public CorporateSsoMembershipProvisioner(ICorporateDbContext db, ILogger<CorporateSsoMembershipProvisioner> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SsoInviteAcceptResult> TryAcceptPendingInviteAsync(
        Guid companyId,
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(userEmail))
            return new SsoInviteAcceptResult(false, null, "no_pending_invite");

        var normalizedEmail = userEmail.Trim().ToLowerInvariant();

        var company = await _db.Companies
            .Include(c => c.Invitations)
            .Include(c => c.Memberships)
            .FirstOrDefaultAsync(c => c.Id == companyId && !c.IsDeleted, cancellationToken);

        if (company is null || !company.IsActive)
            return new SsoInviteAcceptResult(false, null, "company_inactive");

        // Already member → success path for SSO mint (already_member is ok to continue)
        var existing = company.Memberships.FirstOrDefault(m =>
            m.UserId == userId && !m.IsDeleted && m.IsActive);
        if (existing is not null)
            return new SsoInviteAcceptResult(true, existing.Role.ToString(), null);

        var invite = company.Invitations
            .Where(i => !i.IsDeleted
                && string.Equals(i.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)
                && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();

        if (invite is null)
            return new SsoInviteAcceptResult(false, null, "no_pending_invite");

        if (invite.IsExpired)
        {
            invite.MarkExpired();
            await _db.SaveChangesAsync(cancellationToken);
            return new SsoInviteAcceptResult(false, null, "invite_expired");
        }

        try
        {
            var membership = company.AcceptInvitation(invite.InvitationToken, userId, normalizedEmail);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "SSO provisioner accepted invite Company={CompanyId} User={UserId} Role={Role}",
                companyId, userId, membership.Role);
            return new SsoInviteAcceptResult(true, membership.Role.ToString(), null);
        }
        catch (Exception ex)
        {
            // Concurrent double-accept: membership may already exist
            var after = await _db.UserCompanyMemberships
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.CompanyId == companyId && m.UserId == userId && !m.IsDeleted && m.IsActive,
                    cancellationToken);

            if (after is not null)
                return new SsoInviteAcceptResult(true, after.Role.ToString(), null);

            _logger.LogWarning(ex, "SSO invite accept failed Company={CompanyId} User={UserId}", companyId, userId);
            return new SsoInviteAcceptResult(false, null, "no_pending_invite");
        }
    }
}
