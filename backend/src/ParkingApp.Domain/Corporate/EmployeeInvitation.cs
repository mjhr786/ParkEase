using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Corporate;

/// <summary>
/// Represents an invitation for a user to join a company.
/// Supports the hybrid registration flow: link existing user or invite new user by email.
/// </summary>
public class EmployeeInvitation : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string InvitationToken { get; private set; } = string.Empty;
    public CompanyRole Role { get; private set; } = CompanyRole.Employee;
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid InvitedByUserId { get; private set; }

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual User InvitedByUser { get; private set; } = null!;

    // Required for EF Core
    private EmployeeInvitation()
    {
    }

    public static EmployeeInvitation Create(
        Guid companyId,
        string email,
        CompanyRole role,
        Guid invitedByUserId,
        int expiresInDays = 7)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Inviter user ID is required.", nameof(invitedByUserId));
        }

        if (expiresInDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInDays), "Expiry must be at least 1 day.");
        }

        return new EmployeeInvitation
        {
            CompanyId = companyId,
            Email = email.Trim().ToLowerInvariant(),
            InvitationToken = GenerateToken(),
            Role = role,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
            InvitedByUserId = invitedByUserId
        };
    }

    public void Accept(Guid userId)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot accept invitation in {Status} status.");
        }

        if (IsExpired)
        {
            Status = InvitationStatus.Expired;
            throw new InvalidOperationException("Invitation has expired.");
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        Status = InvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel invitation in {Status} status.");
        }

        Status = InvitationStatus.Cancelled;
    }

    public void MarkExpired()
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Expired;
        }
    }

    /// <summary>
    /// Renew a pending or expired invitation: new token and expiry window.
    /// </summary>
    public void Resend(int expiresInDays = 7)
    {
        if (Status is InvitationStatus.Accepted or InvitationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot resend invitation in {Status} status.");
        }

        if (expiresInDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresInDays), "Expiry must be at least 1 day.");
        }

        Status = InvitationStatus.Pending;
        InvitationToken = GenerateToken();
        ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays);
        AcceptedByUserId = null;
        AcceptedAt = null;
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsPending => Status == InvitationStatus.Pending && !IsExpired;

    private static string GenerateToken()
    {
        return $"INV-{Guid.NewGuid():N}".ToUpperInvariant();
    }
}
