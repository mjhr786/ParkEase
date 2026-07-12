using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Corporate;

/// <summary>
/// CorporateBooking entity — lean link between corporate module and existing Booking.
/// Does NOT duplicate booking logic. The standard Booking handles check-in/out, payment, status.
/// This entity adds corporate context: company, membership, allocation, slot type, visitor info.
/// </summary>
public class CorporateBooking : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Guid AllocationId { get; private set; }
    public Guid BookingId { get; private set; }
    public CorporateSlotType SlotType { get; private set; }

    // Visitor booking (minimal fields)
    public bool IsVisitorBooking { get; private set; }
    public string? VisitorName { get; private set; }
    public string? VisitorLicensePlate { get; private set; }

    // Access control (owned value object, nullable for non-visitor bookings)
    public AccessPolicy? AccessPolicy { get; private set; }

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual UserCompanyMembership Membership { get; private set; } = null!;
    public virtual ParkingAllocation Allocation { get; private set; } = null!;
    public virtual Booking Booking { get; private set; } = null!;

    // Required for EF Core
    private CorporateBooking()
    {
    }

    /// <summary>
    /// Create a corporate booking for an employee.
    /// </summary>
    public static CorporateBooking CreateEmployeeBooking(
        Guid companyId,
        Guid membershipId,
        Guid allocationId,
        Guid bookingId,
        CorporateSlotType slotType)
    {
        ValidateRequiredIds(companyId, membershipId, allocationId, bookingId);

        return new CorporateBooking
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            AllocationId = allocationId,
            BookingId = bookingId,
            SlotType = slotType,
            IsVisitorBooking = false
        };
    }

    /// <summary>
    /// Create a corporate booking for a visitor.
    /// </summary>
    public static CorporateBooking CreateVisitorBooking(
        Guid companyId,
        Guid membershipId,
        Guid allocationId,
        Guid bookingId,
        string visitorName,
        string visitorLicensePlate,
        AccessPolicy accessPolicy)
    {
        ValidateRequiredIds(companyId, membershipId, allocationId, bookingId);

        if (string.IsNullOrWhiteSpace(visitorName))
        {
            throw new ArgumentException("Visitor name is required.", nameof(visitorName));
        }

        if (string.IsNullOrWhiteSpace(visitorLicensePlate))
        {
            throw new ArgumentException("Visitor license plate is required.", nameof(visitorLicensePlate));
        }

        return new CorporateBooking
        {
            CompanyId = companyId,
            MembershipId = membershipId,
            AllocationId = allocationId,
            BookingId = bookingId,
            SlotType = CorporateSlotType.Shared,
            IsVisitorBooking = true,
            VisitorName = visitorName.Trim(),
            VisitorLicensePlate = visitorLicensePlate.Trim().ToUpperInvariant(),
            AccessPolicy = accessPolicy ?? throw new ArgumentNullException(nameof(accessPolicy))
        };
    }

    private static void ValidateRequiredIds(Guid companyId, Guid membershipId, Guid allocationId, Guid bookingId)
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

        if (bookingId == Guid.Empty)
        {
            throw new ArgumentException("Booking ID is required.", nameof(bookingId));
        }
    }
}
