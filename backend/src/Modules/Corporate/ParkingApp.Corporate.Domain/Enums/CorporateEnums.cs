namespace ParkingApp.Domain.Enums;

// Corporate-owned enums live in Corporate.Domain assembly while keeping the historical
// ParkingApp.Domain.Enums namespace so EF configurations and API contracts stay stable.

/// <summary>
/// Role within a company context. Stored in UserCompanyMembership,
/// NOT in the global User entity. A user can be Admin in one company
/// and Employee in another.
/// </summary>
public enum CompanyRole
{
    Employee = 0,
    Admin = 1
}

/// <summary>
/// How a company is billed for parking.
/// </summary>
public enum BillingType
{
    /// <summary>Flat monthly rate based on reserved slot count.</summary>
    ReservedSlots = 0,
    /// <summary>Billed based on actual usage (hours parked).</summary>
    UsageBased = 1
}

/// <summary>
/// Type of corporate parking slot.
/// </summary>
public enum CorporateSlotType
{
    /// <summary>Assigned to a specific employee, always available to them.</summary>
    Fixed = 0,
    /// <summary>Pool-based booking, first come first served within quota.</summary>
    Shared = 1
}

/// <summary>
/// Status of a parking allocation (company → parking space contract).
/// Requires parking space owner approval before activation.
/// </summary>
public enum AllocationStatus
{
    PendingApproval = 0,
    Active = 1,
    Rejected = 2,
    Expired = 3
}

public enum ParkingAllocationSource
{
    VendorLease = 0,
    CompanyOwned = 1
}

/// <summary>
/// Status of an employee invitation to join a company.
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Cancelled = 3
}

/// <summary>
/// Status of a corporate waitlist entry.
/// </summary>
public enum WaitlistStatus
{
    Pending = 0,
    Promoted = 1,
    Cancelled = 2
}

/// <summary>
/// Risk level for suspicious corporate booking behavior.
/// </summary>
public enum CorporateFraudRiskLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>
/// Lifecycle of a company billing invoice (period settlement).
/// </summary>
public enum CorporateInvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Void = 3
}

/// <summary>
/// Type of charge line on a corporate invoice.
/// </summary>
public enum CorporateInvoiceLineType
{
    /// <summary>Prorated reserved capacity from vendor lease MonthlyRate.</summary>
    ReservedCapacity = 0,
    /// <summary>Usage charge from corporate booking TotalAmount.</summary>
    Usage = 1
}
