using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.Domain.Entities;

/// <summary>
/// Links a ParkEase user to an enterprise IdP subject for a specific company.
/// Marketplace social login must not use this entity (see <see cref="UserExternalLogin"/>).
/// </summary>
public class CorporateSsoIdentityLink : BaseEntity
{
    public Guid CompanyId { get; internal set; }
    public Guid UserId { get; internal set; }
    public SsoProtocol Protocol { get; internal set; }
    public string Subject { get; internal set; } = string.Empty;
    public string IdPIssuer { get; internal set; } = string.Empty;
    public string? ProviderEmail { get; internal set; }
    public DateTime LinkedAtUtc { get; internal set; }
    public DateTime? LastUsedAtUtc { get; internal set; }
    public bool IsDisabled { get; internal set; }

    public virtual User User { get; internal set; } = null!;

    internal CorporateSsoIdentityLink()
    {
    }

    public static CorporateSsoIdentityLink Create(
        Guid companyId,
        Guid userId,
        SsoProtocol protocol,
        string subject,
        string idpIssuer,
        string? providerEmail = null,
        DateTime? linkedAtUtc = null)
    {
        if (companyId == Guid.Empty)
            throw new ValidationException("companyId", "Company id is required");
        if (userId == Guid.Empty)
            throw new ValidationException("userId", "User id is required");
        if (string.IsNullOrWhiteSpace(subject))
            throw new ValidationException("subject", "IdP subject is required");
        if (string.IsNullOrWhiteSpace(idpIssuer))
            throw new ValidationException("idpIssuer", "IdP issuer is required");

        var now = linkedAtUtc ?? DateTime.UtcNow;
        return new CorporateSsoIdentityLink
        {
            CompanyId = companyId,
            UserId = userId,
            Protocol = protocol,
            Subject = subject.Trim(),
            IdPIssuer = idpIssuer.Trim(),
            ProviderEmail = string.IsNullOrWhiteSpace(providerEmail) ? null : providerEmail.Trim(),
            LinkedAtUtc = now,
            LastUsedAtUtc = now,
            IsDisabled = false
        };
    }

    public void RecordUse(string? providerEmail = null, DateTime? usedAtUtc = null)
    {
        LastUsedAtUtc = usedAtUtc ?? DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(providerEmail))
            ProviderEmail = providerEmail.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProviderEmail(string? email)
    {
        ProviderEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsDisabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsDisabled = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
