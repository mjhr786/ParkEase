using Microsoft.EntityFrameworkCore;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Infrastructure.Persistence;

namespace ParkingApp.Identity.Infrastructure.Services.Sso;

/// <summary>Company-admin soft-disable of Corporate SSO identity links.</summary>
public sealed class CorporateSsoLinkAdmin : ICorporateSsoLinkAdmin
{
    private readonly IIdentityDbContext _db;

    public CorporateSsoLinkAdmin(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<bool> DisableLinkAsync(
        Guid companyId,
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _db.CorporateSsoIdentityLinks
            .FirstOrDefaultAsync(l =>
                l.Id == linkId
                && l.CompanyId == companyId
                && !l.IsDeleted, cancellationToken);

        if (link is null)
            return false;

        link.Disable();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
