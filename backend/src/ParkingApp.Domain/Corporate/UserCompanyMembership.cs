using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Corporate;

/// <summary>
/// Represents a user's membership in a company.
/// Replaces a traditional Employee entity to support multi-company membership.
/// A single user can be Admin in Company A and Employee in Company B.
/// </summary>
public class UserCompanyMembership : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public CompanyRole Role { get; private set; }
    public string? EmployeeCode { get; private set; }
    public int Priority { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual User User { get; private set; } = null!;

    // Required for EF Core
    private UserCompanyMembership()
    {
    }

    private UserCompanyMembership(Guid companyId, Guid userId, CompanyRole role, string? employeeCode, int priority)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (priority < 1 || priority > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 1 and 10.");
        }

        CompanyId = companyId;
        UserId = userId;
        Role = role;
        EmployeeCode = employeeCode?.Trim();
        Priority = priority;
    }

    internal static UserCompanyMembership Create(Guid companyId, Guid userId, CompanyRole role, string? employeeCode = null, int priority = 1)
    {
        return new UserCompanyMembership(companyId, userId, role, employeeCode, priority);
    }

    public void SetRole(CompanyRole role)
    {
        Role = role;
    }

    public void SetPriority(int priority)
    {
        if (priority < 1 || priority > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 1 and 10.");
        }

        Priority = priority;
    }

    public void SetEmployeeCode(string? employeeCode)
    {
        EmployeeCode = string.IsNullOrWhiteSpace(employeeCode) ? null : employeeCode.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
        IsDeleted = true;
    }

    public void Activate()
    {
        IsActive = true;
        IsDeleted = false;
    }

    public bool IsAdmin => Role == CompanyRole.Admin;
}
