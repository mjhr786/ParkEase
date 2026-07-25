namespace ParkingApp.Marketplace.Contracts.Enums;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Expired = 5,
    AwaitingPayment = 6,
    Rejected = 7,
    PendingExtension = 8,
    AwaitingExtensionPayment = 9
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

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    UPI = 2,
    NetBanking = 3,
    Wallet = 4,
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

