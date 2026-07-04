namespace ParkingApp.Domain.Enums;

public enum UserRole
{
    Admin = 0,
    User = 1
}

public enum BookingStatus
{
    Pending = 0,                   // Waiting for owner approval
    Confirmed = 1,                 // Owner approved AND payment completed
    InProgress = 2,                // Checked in
    Completed = 3,                 // Checked out
    Cancelled = 4,                 // Cancelled by user or owner
    Expired = 5,                   // Booking expired
    AwaitingPayment = 6,           // Owner approved initial booking, waiting for member payment
    Rejected = 7,                  // Rejected by owner
    PendingExtension = 8,          // Extension requested by user, awaiting owner approval
    AwaitingExtensionPayment = 9   // Owner approved extension, waiting for member payment
}

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
    PartialRefund = 4
}

public enum PricingType
{
    Hourly = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum ParkingType
{
    Open = 0,
    Covered = 1,
    Garage = 2,
    Street = 3,
    Underground = 4
}

public enum ParkingSpaceOwnershipType
{
    IndividualVendor = 0,
    CompanyOwned = 1
}

public enum VehicleType
{
    Car = 0,
    Motorcycle = 1,
    SUV = 2,
    Truck = 3,
    Van = 4,
    Electric = 5
}

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    UPI = 2,
    NetBanking = 3,
    Wallet = 4,
}

public enum NotificationType
{
    BookingRequest = 0,
    BookingConfirmed = 1,
    BookingRejected = 2,
    PaymentReceived = 3,
    NewMessage = 4,
    SystemAlert = 5
}

public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum PassTypeKind
{
    Monthly = 0,
    Weekly = 1,
    Corporate = 2
}

public enum PassCoverageType
{
    ParkingSpace = 0,
    ParkingZone = 1
}

public enum PassUsageMode
{
    UnlimitedEntries = 0,
    LimitedHoursPerDay = 1
}

// ══════════════════════════════════════════════════════
// CORPORATE MODULE ENUMS
// ══════════════════════════════════════════════════════

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
    /// <summary>
    /// Flat monthly rate based on reserved slot count.
    /// </summary>
    ReservedSlots = 0,
    /// <summary>
    /// Billed based on actual usage (hours parked).
    /// </summary>
    UsageBased = 1
}

/// <summary>
/// Type of corporate parking slot.
/// </summary>
public enum CorporateSlotType
{
    /// <summary>
    /// Assigned to a specific employee, always available to them.
    /// </summary>
    Fixed = 0,
    /// <summary>
    /// Pool-based booking, first come first served within quota.
    /// </summary>
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
