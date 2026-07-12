using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Corporate;

public class CorporateWaitlistEntry : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid AllocationId { get; private set; }
    public WaitlistStatus Status { get; private set; } = WaitlistStatus.Pending;
    public bool IsVisitorBooking { get; private set; }
    public DateTime RequestedStartDateTime { get; private set; }
    public DateTime RequestedEndDateTime { get; private set; }
    public VehicleType VehicleType { get; private set; } = VehicleType.Car;
    public string? VehicleNumber { get; private set; }
    public string? VisitorName { get; private set; }
    public string? VisitorLicensePlate { get; private set; }
    public DateTime? AccessExpiryUtc { get; private set; }
    public int PriorityAtRequest { get; private set; }
    public Guid? PromotedBookingId { get; private set; }
    public DateTime? PromotedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public virtual Company Company { get; private set; } = null!;
    public virtual UserCompanyMembership Membership { get; private set; } = null!;
    public virtual ParkingAllocation Allocation { get; private set; } = null!;
    public virtual Booking? PromotedBooking { get; private set; }

    private CorporateWaitlistEntry()
    {
    }

    public static CorporateWaitlistEntry CreateEmployee(
        Guid companyId,
        Guid membershipId,
        Guid allocationId,
        DateTime requestedStartDateTime,
        DateTime requestedEndDateTime,
        VehicleType vehicleType,
        string? vehicleNumber,
        int priorityAtRequest)
    {
        ValidateRequiredIds(companyId, membershipId, allocationId);
        ValidateRequestedWindow(requestedStartDateTime, requestedEndDateTime);
        ValidatePriority(priorityAtRequest);

        return new CorporateWaitlistEntry
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            AllocationId = allocationId,
            RequestedStartDateTime = NormalizeDate(requestedStartDateTime),
            RequestedEndDateTime = NormalizeDate(requestedEndDateTime),
            VehicleType = vehicleType,
            VehicleNumber = NormalizeVehicleNumber(vehicleNumber),
            PriorityAtRequest = priorityAtRequest,
            IsVisitorBooking = false
        };
    }

    public static CorporateWaitlistEntry CreateVisitor(
        Guid companyId,
        Guid membershipId,
        Guid allocationId,
        DateTime requestedStartDateTime,
        DateTime requestedEndDateTime,
        string visitorName,
        string visitorLicensePlate,
        DateTime accessExpiryUtc,
        int priorityAtRequest)
    {
        ValidateRequiredIds(companyId, membershipId, allocationId);
        ValidateRequestedWindow(requestedStartDateTime, requestedEndDateTime);
        ValidatePriority(priorityAtRequest);

        if (string.IsNullOrWhiteSpace(visitorName))
        {
            throw new ArgumentException("Visitor name is required.", nameof(visitorName));
        }

        if (string.IsNullOrWhiteSpace(visitorLicensePlate))
        {
            throw new ArgumentException("Visitor license plate is required.", nameof(visitorLicensePlate));
        }

        var normalizedStart = NormalizeDate(requestedStartDateTime);
        var normalizedEnd = NormalizeDate(requestedEndDateTime);
        var normalizedAccessExpiry = NormalizeDate(accessExpiryUtc);
        if (normalizedAccessExpiry < normalizedEnd)
        {
            throw new ArgumentException("Visitor access expiry must not be earlier than the booking end time.", nameof(accessExpiryUtc));
        }

        return new CorporateWaitlistEntry
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            AllocationId = allocationId,
            RequestedStartDateTime = normalizedStart,
            RequestedEndDateTime = normalizedEnd,
            VehicleType = VehicleType.Car,
            VehicleNumber = NormalizeVehicleNumber(visitorLicensePlate),
            VisitorName = visitorName.Trim(),
            VisitorLicensePlate = NormalizeVehicleNumber(visitorLicensePlate),
            AccessExpiryUtc = normalizedAccessExpiry,
            PriorityAtRequest = priorityAtRequest,
            IsVisitorBooking = true
        };
    }

    public void Promote(Guid bookingId)
    {
        if (Status != WaitlistStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot promote waitlist entry in {Status} status.");
        }

        if (bookingId == Guid.Empty)
        {
            throw new ArgumentException("Booking ID is required.", nameof(bookingId));
        }

        Status = WaitlistStatus.Promoted;
        PromotedBookingId = bookingId;
        PromotedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != WaitlistStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel waitlist entry in {Status} status.");
        }

        Status = WaitlistStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
    }

    public bool Overlaps(DateTime startUtc, DateTime endUtc)
    {
        var normalizedStart = NormalizeDate(startUtc);
        var normalizedEnd = NormalizeDate(endUtc);
        return RequestedStartDateTime < normalizedEnd && RequestedEndDateTime > normalizedStart;
    }

    public bool MatchesEmployeeRequest(Guid membershipId, DateTime startUtc, DateTime endUtc, string? vehicleNumber)
    {
        return !IsVisitorBooking
            && MembershipId == membershipId
            && RequestedStartDateTime == NormalizeDate(startUtc)
            && RequestedEndDateTime == NormalizeDate(endUtc)
            && string.Equals(VehicleNumber, NormalizeVehicleNumber(vehicleNumber), StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesVisitorRequest(Guid membershipId, DateTime startUtc, DateTime endUtc, string visitorLicensePlate)
    {
        return IsVisitorBooking
            && MembershipId == membershipId
            && RequestedStartDateTime == NormalizeDate(startUtc)
            && RequestedEndDateTime == NormalizeDate(endUtc)
            && string.Equals(VisitorLicensePlate, NormalizeVehicleNumber(visitorLicensePlate), StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRequiredIds(Guid companyId, Guid membershipId, Guid allocationId)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        if (allocationId == Guid.Empty)
        {
            throw new ArgumentException("Allocation ID is required.", nameof(allocationId));
        }
    }

    private static void ValidateRequestedWindow(DateTime startUtc, DateTime endUtc)
    {
        var normalizedStart = NormalizeDate(startUtc);
        var normalizedEnd = NormalizeDate(endUtc);
        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("Waitlist end time must be after the start time.");
        }
    }

    private static void ValidatePriority(int priorityAtRequest)
    {
        if (priorityAtRequest < 1 || priorityAtRequest > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(priorityAtRequest), "Priority must be between 1 and 10.");
        }
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeVehicleNumber(string? vehicleNumber)
    {
        return string.IsNullOrWhiteSpace(vehicleNumber)
            ? null
            : vehicleNumber.Trim().ToUpperInvariant();
    }
}
