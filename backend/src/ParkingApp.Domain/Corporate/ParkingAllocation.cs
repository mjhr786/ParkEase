using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Corporate;

/// <summary>
/// ParkingAllocation Aggregate — represents a company's parking contract at a specific location.
/// Owns fixed slot assignments. Contains BookingPolicy for per-location rules.
/// Requires parking space owner approval before activation.
/// </summary>
public class ParkingAllocation : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ParkingSpaceId { get; private set; }

    // Quota (owned value object)
    public Quota Quota { get; private set; } = Quota.Create(1, 0, 1);

    // Contract
    public ParkingAllocationSource SourceType { get; private set; } = ParkingAllocationSource.VendorLease;
    public Guid? VendorId { get; private set; }
    public string? LeaseReference { get; private set; }
    public decimal MonthlyRate { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    // Approval
    public AllocationStatus Status { get; private set; } = AllocationStatus.PendingApproval;
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    // Rules (owned value object — per-allocation)
    public BookingPolicy BookingPolicy { get; private set; } = BookingPolicy.Default();

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; private set; } = null!;
    public virtual User? ApprovedByUser { get; private set; }
    public virtual ICollection<FixedSlotAssignment> FixedAssignments { get; private set; } = new List<FixedSlotAssignment>();
    public virtual ICollection<CorporateBooking> CorporateBookings { get; private set; } = new List<CorporateBooking>();

    // Required for EF Core
    private ParkingAllocation()
    {
    }

    private ParkingAllocation(
        Guid companyId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        BookingPolicy? bookingPolicy)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (parkingSpaceId == Guid.Empty)
        {
            throw new ArgumentException("Parking space ID is required.", nameof(parkingSpaceId));
        }

        if (monthlyRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), "Monthly rate cannot be negative.");
        }

        var normalizedStart = NormalizeDate(startDate);
        var normalizedEnd = NormalizeDate(endDate);

        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("End date must be after start date.");
        }

        CompanyId = companyId;
        ParkingSpaceId = parkingSpaceId;
        Quota = quota ?? throw new ArgumentNullException(nameof(quota));
        MonthlyRate = Math.Round(monthlyRate, 2, MidpointRounding.AwayFromZero);
        StartDate = normalizedStart;
        EndDate = normalizedEnd;
        BookingPolicy = bookingPolicy ?? BookingPolicy.Default();
    }

    public static ParkingAllocation Create(
        Guid companyId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        BookingPolicy? bookingPolicy = null)
    {
        var allocation = new ParkingAllocation(companyId, parkingSpaceId, quota, monthlyRate, startDate, endDate, bookingPolicy);
        allocation.SourceType = ParkingAllocationSource.VendorLease;
        return allocation;
    }

    public void SetVendorLeaseMetadata(Guid vendorId, string? leaseReference)
    {
        if (SourceType != ParkingAllocationSource.VendorLease)
        {
            throw new InvalidOperationException("Lease metadata can only be applied to vendor-leased allocations.");
        }

        if (vendorId == Guid.Empty)
        {
            throw new ArgumentException("Vendor ID is required.", nameof(vendorId));
        }

        VendorId = vendorId;
        LeaseReference = string.IsNullOrWhiteSpace(leaseReference) ? null : leaseReference.Trim();
    }

    public static ParkingAllocation CreateCompanyOwned(
        Guid companyId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        Guid approvedByUserId,
        BookingPolicy? bookingPolicy = null)
    {
        var allocation = new ParkingAllocation(companyId, parkingSpaceId, quota, monthlyRate, startDate, endDate, bookingPolicy);
        allocation.SourceType = ParkingAllocationSource.CompanyOwned;
        allocation.Status = AllocationStatus.Active;
        allocation.ApprovedByUserId = approvedByUserId;
        allocation.ApprovedAt = DateTime.UtcNow;
        return allocation;
    }

    public void Approve(Guid approvedByUserId)
    {
        if (Status != AllocationStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot approve allocation in {Status} status.");
        }

        if (approvedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Approver user ID is required.", nameof(approvedByUserId));
        }

        Status = AllocationStatus.Active;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
    }

    public void Reject(string? reason)
    {
        if (Status != AllocationStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot reject allocation in {Status} status.");
        }

        Status = AllocationStatus.Rejected;
        RejectionReason = reason?.Trim();
    }

    public void Expire()
    {
        Status = AllocationStatus.Expired;
    }

    public void AssignFixedSlot(UserCompanyMembership membership, int slotNumber)
    {
        if (membership == null)
        {
            throw new ArgumentNullException(nameof(membership));
        }

        if (Status != AllocationStatus.Active)
        {
            throw new InvalidOperationException("Can only assign slots to active allocations.");
        }

        if (membership.CompanyId != CompanyId)
        {
            throw new InvalidOperationException("Fixed slots can only be assigned to members of the same company.");
        }

        if (!membership.IsActive || membership.IsDeleted)
        {
            throw new InvalidOperationException("Only active company members can receive fixed slots.");
        }

        if (slotNumber < 1 || slotNumber > Quota.FixedSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), $"Slot number must be between 1 and {Quota.FixedSlots}.");
        }

        if (FixedAssignments.Any(a => a.SlotNumber == slotNumber && !a.IsDeleted))
        {
            throw new InvalidOperationException($"Slot {slotNumber} is already assigned.");
        }

        if (FixedAssignments.Any(a => a.MembershipId == membership.Id && !a.IsDeleted))
        {
            throw new InvalidOperationException("This member already has a fixed slot assignment.");
        }

        var assignment = FixedSlotAssignment.Create(CompanyId, Id, membership.Id, slotNumber);
        FixedAssignments.Add(assignment);
    }

    public void RemoveFixedAssignment(Guid membershipId)
    {
        var assignment = FixedAssignments.FirstOrDefault(a => a.MembershipId == membershipId && !a.IsDeleted);
        if (assignment == null)
        {
            throw new InvalidOperationException("No fixed slot assignment found for this member.");
        }

        assignment.IsDeleted = true;
    }

    public bool HasFixedSlotAssignment(Guid membershipId)
    {
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        return FixedAssignments.Any(a => a.MembershipId == membershipId && !a.IsDeleted);
    }

    /// <summary>
    /// Calculate available shared slots given the current occupancy footprint.
    /// </summary>
    public int GetAvailableSharedSlots(IReadOnlyCollection<int> occupiedSharedSlotNumbers, int anonymousOccupiedSharedBookings = 0)
    {
        if (Status != AllocationStatus.Active)
        {
            return 0;
        }

        var explicitOccupancy = occupiedSharedSlotNumbers?.Count ?? 0;
        var currentOccupancy = explicitOccupancy + Math.Max(0, anonymousOccupiedSharedBookings);
        return Math.Max(0, Quota.SharedSlots - currentOccupancy);
    }

    public void EnsureEmployeeBookingAllowed(
        int memberPriority,
        DateTime bookingStart,
        DateTime bookingEnd,
        int currentDayBookings,
        int currentWeekBookings)
    {
        EnsureBookingWindow(bookingStart, bookingEnd);

        if (!BookingPolicy.IsWithinDailyLimit(currentDayBookings))
        {
            throw new InvalidOperationException("Daily booking limit reached for this member.");
        }

        if (!BookingPolicy.IsWithinWeeklyLimit(currentWeekBookings))
        {
            throw new InvalidOperationException("Weekly booking limit reached for this member.");
        }

        if (!BookingPolicy.MeetsPriorityRequirement(memberPriority))
        {
            throw new InvalidOperationException("Member priority does not meet this allocation's booking threshold.");
        }
    }

    public void EnsureVisitorBookingAllowed(DateTime bookingStart, DateTime bookingEnd)
    {
        EnsureBookingWindow(bookingStart, bookingEnd);
    }

    public CorporateSlotReservation ResolveSlotReservation(
        Guid membershipId,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
    {
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        var fixedAssignment = FixedAssignments.FirstOrDefault(a => a.MembershipId == membershipId && !a.IsDeleted);
        if (fixedAssignment != null)
        {
            return CorporateSlotReservation.Fixed(fixedAssignment.SlotNumber);
        }

        return ResolveSharedSlotReservation(occupiedSharedSlotNumbers, sharedSlotUsageBySlot, anonymousOccupiedSharedBookings);
    }

    public CorporateSlotReservation ResolveSharedSlotReservation(
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
    {
        if (Status != AllocationStatus.Active)
        {
            throw new InvalidOperationException("Can only book against an active allocation.");
        }

        var candidateSlots = GetSharedSlotNumbers().Except(occupiedSharedSlotNumbers ?? Array.Empty<int>()).ToList();
        if (candidateSlots.Count <= Math.Max(0, anonymousOccupiedSharedBookings))
        {
            throw new InvalidOperationException("No shared parking slots available for the requested time.");
        }

        var usageBySlot = sharedSlotUsageBySlot ?? new Dictionary<int, int>();
        var selectedSlot = candidateSlots
            .OrderBy(slot => usageBySlot.TryGetValue(slot, out var usage) ? usage : 0)
            .ThenBy(slot => slot)
            .Skip(Math.Max(0, anonymousOccupiedSharedBookings))
            .First();

        return CorporateSlotReservation.Shared(selectedSlot);
    }

    /// <summary>
    /// Validate whether a booking is allowed based on the allocation's BookingPolicy.
    /// All business rules enforced in domain.
    /// </summary>
    public bool IsBookingAllowed(
        int memberPriority,
        DateTime bookingStart,
        DateTime bookingEnd,
        int currentDayBookings,
        int currentWeekBookings)
    {
        try
        {
            EnsureEmployeeBookingAllowed(memberPriority, bookingStart, bookingEnd, currentDayBookings, currentWeekBookings);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void UpdateBookingPolicy(BookingPolicy policy)
    {
        BookingPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>
    /// Update lease / contract terms (rate, window, optional reference).
    /// Allowed for pending or active allocations only.
    /// </summary>
    public void UpdateContractTerms(
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        string? leaseReference)
    {
        if (Status is AllocationStatus.Rejected or AllocationStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot update contract terms for allocation in {Status} status.");
        }

        if (monthlyRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), "Monthly rate cannot be negative.");
        }

        var normalizedStart = NormalizeDate(startDate);
        var normalizedEnd = NormalizeDate(endDate);

        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("End date must be after start date.");
        }

        MonthlyRate = Math.Round(monthlyRate, 2, MidpointRounding.AwayFromZero);
        StartDate = normalizedStart;
        EndDate = normalizedEnd;
        LeaseReference = string.IsNullOrWhiteSpace(leaseReference) ? null : leaseReference.Trim();
    }

    public bool IsActiveAllocation => Status == AllocationStatus.Active && !IsDeleted;

    private IEnumerable<int> GetSharedSlotNumbers()
    {
        if (Quota.SharedSlots <= 0)
        {
            return Enumerable.Empty<int>();
        }

        return Enumerable.Range(Quota.FixedSlots + 1, Quota.SharedSlots);
    }

    private void EnsureBookingWindow(DateTime bookingStart, DateTime bookingEnd)
    {
        if (Status != AllocationStatus.Active)
        {
            throw new InvalidOperationException("Can only book against an active allocation.");
        }

        var normalizedStart = NormalizeDate(bookingStart);
        var normalizedEnd = NormalizeDate(bookingEnd);

        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("Booking end time must be after the start time.");
        }

        if (normalizedStart < StartDate || normalizedEnd > EndDate)
        {
            throw new InvalidOperationException("The requested booking window falls outside the allocation contract period.");
        }

        if (!BookingPolicy.IsWeekendAllowed(normalizedStart))
        {
            throw new InvalidOperationException("Weekend bookings are not allowed for this allocation.");
        }

        if (!BookingPolicy.IsWithinTimeRestrictions(normalizedStart, normalizedEnd))
        {
            throw new InvalidOperationException("The requested booking time falls outside the allocation's allowed hours.");
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
}
