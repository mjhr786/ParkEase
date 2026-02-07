namespace ParkingApp.Domain.Enums;

public enum UserRole
{
    Admin = 0,
    Vendor = 1,
    Member = 2
}

public enum BookingStatus
{
    Pending = 0,          // Waiting for owner approval
    Confirmed = 1,        // Owner approved AND payment completed
    InProgress = 2,       // Checked in
    Completed = 3,        // Checked out
    Cancelled = 4,        // Cancelled by user or owner
    Expired = 5,          // Booking expired
    AwaitingPayment = 6,  // Owner approved, waiting for member payment
    Rejected = 7          // Rejected by owner
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
    Cash = 5
}
