// Navigation properties removed for strict module isolation
using System.Diagnostics.CodeAnalysis;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Corporate.Domain;

/// <summary>
/// ParkingAllocation Aggregate — a company's parking contract at a specific location.
/// Owns fixed slot assignments and dual capacity pools (2W / 4W).
/// Contains BookingPolicy for per-location rules.
/// Requires parking space owner approval before activation (vendor lease).
/// </summary>
public class ParkingAllocation : BaseEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>Combined mirror (TwoWheeler + FourWheeler) for legacy columns and reporting.</summary>
    public Quota Quota { get; private set; } = Quota.Create(1, 0, 1);

    /// <summary>2-wheeler capacity pool (bike/scooter). May be empty.</summary>
    public Quota TwoWheelerQuota { get; private set; } = Quota.None;

    /// <summary>4-wheeler capacity pool (car/SUV/etc). May be empty only if 2W has capacity.</summary>
    public Quota FourWheelerQuota { get; private set; } = Quota.Create(1, 0, 1);

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

    // External Aggregate IDs
    public Guid ParkingSpaceId { get; private set; }
    public virtual Company Company { get; private set; } = null!;
    public virtual ICollection<FixedSlotAssignment> FixedAssignments { get; private set; } = new List<FixedSlotAssignment>();
    public virtual ICollection<CorporateBooking> CorporateBookings { get; private set; } = new List<CorporateBooking>();

    // Required for EF Core materialization — no business logic.
    [ExcludeFromCodeCoverage]
    private ParkingAllocation()
    {
    }

    private ParkingAllocation(
        Guid companyId,
        Guid parkingSpaceId,
        Quota twoWheelerQuota,
        Quota fourWheelerQuota,
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
        SetClassQuotas(twoWheelerQuota, fourWheelerQuota);
        MonthlyRate = Math.Round(monthlyRate, 2, MidpointRounding.AwayFromZero);
        StartDate = normalizedStart;
        EndDate = normalizedEnd;
        BookingPolicy = bookingPolicy ?? BookingPolicy.Default();
    }

    /// <summary>
    /// Legacy single-pool factory: treats the quota as FourWheeler-only (2W empty).
    /// </summary>
    public static ParkingAllocation Create(
        Guid companyId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        BookingPolicy? bookingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(quota);
        var allocation = new ParkingAllocation(
            companyId, parkingSpaceId, Quota.None, quota, monthlyRate, startDate, endDate, bookingPolicy);
        allocation.SourceType = ParkingAllocationSource.VendorLease;
        return allocation;
    }

    /// <summary>Dual-pool vendor lease allocation.</summary>
    public static ParkingAllocation Create(
        Guid companyId,
        Guid parkingSpaceId,
        Quota twoWheelerQuota,
        Quota fourWheelerQuota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        BookingPolicy? bookingPolicy = null)
    {
        var allocation = new ParkingAllocation(
            companyId, parkingSpaceId, twoWheelerQuota, fourWheelerQuota, monthlyRate, startDate, endDate, bookingPolicy);
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

    /// <summary>
    /// Legacy single-pool factory: treats the quota as FourWheeler-only.
    /// </summary>
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
        ArgumentNullException.ThrowIfNull(quota);
        var allocation = new ParkingAllocation(
            companyId, parkingSpaceId, Quota.None, quota, monthlyRate, startDate, endDate, bookingPolicy);
        allocation.SourceType = ParkingAllocationSource.CompanyOwned;
        allocation.Status = AllocationStatus.Active;
        allocation.ApprovedByUserId = approvedByUserId;
        allocation.ApprovedAt = DateTime.UtcNow;
        return allocation;
    }

    public static ParkingAllocation CreateCompanyOwned(
        Guid companyId,
        Guid parkingSpaceId,
        Quota twoWheelerQuota,
        Quota fourWheelerQuota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        Guid approvedByUserId,
        BookingPolicy? bookingPolicy = null)
    {
        var allocation = new ParkingAllocation(
            companyId, parkingSpaceId, twoWheelerQuota, fourWheelerQuota, monthlyRate, startDate, endDate, bookingPolicy);
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

    public Quota GetQuota(VehicleClass vehicleClass) => vehicleClass switch
    {
        VehicleClass.TwoWheeler => TwoWheelerQuota,
        VehicleClass.FourWheeler => FourWheelerQuota,
        _ => throw new ArgumentOutOfRangeException(nameof(vehicleClass))
    };

    public void EnsureClassOffered(VehicleClass vehicleClass)
    {
        if (GetQuota(vehicleClass).IsEmpty)
        {
            var label = vehicleClass == VehicleClass.TwoWheeler ? "2-wheeler" : "4-wheeler";
            throw new InvalidOperationException($"This allocation does not offer {label} parking.");
        }
    }

    /// <summary>
    /// Assign fixed slot for a vehicle class (default FourWheeler for legacy callers).
    /// </summary>
    public void AssignFixedSlot(UserCompanyMembership membership, int slotNumber)
        => AssignFixedSlot(membership, VehicleClass.FourWheeler, slotNumber);

    public void AssignFixedSlot(UserCompanyMembership membership, VehicleClass vehicleClass, int slotNumber)
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

        var pool = GetQuota(vehicleClass);
        if (pool.FixedSlots <= 0)
        {
            throw new InvalidOperationException("This allocation has no fixed slots for the selected vehicle class.");
        }

        if (slotNumber < 1 || slotNumber > pool.FixedSlots)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotNumber),
                $"Slot number must be between 1 and {pool.FixedSlots} for {vehicleClass}.");
        }

        if (FixedAssignments.Any(a => a.VehicleClass == vehicleClass && a.SlotNumber == slotNumber && !a.IsDeleted))
        {
            throw new InvalidOperationException($"Slot {slotNumber} is already assigned for {vehicleClass}.");
        }

        if (FixedAssignments.Any(a => a.MembershipId == membership.Id && a.VehicleClass == vehicleClass && !a.IsDeleted))
        {
            throw new InvalidOperationException("This member already has a fixed slot assignment for this vehicle class.");
        }

        var assignment = FixedSlotAssignment.Create(CompanyId, Id, membership.Id, vehicleClass, slotNumber);
        FixedAssignments.Add(assignment);
    }

    public void RemoveFixedAssignment(Guid membershipId)
    {
        var assignments = FixedAssignments.Where(a => a.MembershipId == membershipId && !a.IsDeleted).ToList();
        if (assignments.Count == 0)
        {
            throw new InvalidOperationException("No fixed slot assignment found for this member.");
        }

        foreach (var assignment in assignments)
        {
            assignment.IsDeleted = true;
        }
    }

    public void RemoveFixedAssignment(Guid membershipId, VehicleClass vehicleClass)
    {
        var assignment = FixedAssignments.FirstOrDefault(
            a => a.MembershipId == membershipId && a.VehicleClass == vehicleClass && !a.IsDeleted);
        if (assignment == null)
        {
            throw new InvalidOperationException("No fixed slot assignment found for this member and vehicle class.");
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

    public bool HasFixedSlotAssignment(Guid membershipId, VehicleClass vehicleClass)
    {
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        return FixedAssignments.Any(
            a => a.MembershipId == membershipId && a.VehicleClass == vehicleClass && !a.IsDeleted);
    }

    /// <summary>
    /// Calculate available shared slots for a vehicle class given occupancy.
    /// </summary>
    public int GetAvailableSharedSlots(
        VehicleClass vehicleClass,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        int anonymousOccupiedSharedBookings = 0)
    {
        if (Status != AllocationStatus.Active)
        {
            return 0;
        }

        var pool = GetQuota(vehicleClass);
        if (pool.SharedSlots <= 0)
        {
            return 0;
        }

        var explicitOccupancy = occupiedSharedSlotNumbers?.Count ?? 0;
        var currentOccupancy = explicitOccupancy + Math.Max(0, anonymousOccupiedSharedBookings);
        return Math.Max(0, pool.SharedSlots - currentOccupancy);
    }

    /// <summary>Legacy helper: uses FourWheeler pool.</summary>
    public int GetAvailableSharedSlots(
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        int anonymousOccupiedSharedBookings = 0)
        => GetAvailableSharedSlots(VehicleClass.FourWheeler, occupiedSharedSlotNumbers, anonymousOccupiedSharedBookings);

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
        VehicleClass vehicleClass,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
    {
        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        EnsureClassOffered(vehicleClass);

        var fixedAssignment = FixedAssignments.FirstOrDefault(
            a => a.MembershipId == membershipId && a.VehicleClass == vehicleClass && !a.IsDeleted);
        if (fixedAssignment != null)
        {
            return CorporateSlotReservation.Fixed(fixedAssignment.SlotNumber);
        }

        return ResolveSharedSlotReservation(
            vehicleClass, occupiedSharedSlotNumbers, sharedSlotUsageBySlot, anonymousOccupiedSharedBookings);
    }

    /// <summary>Legacy helper: FourWheeler pool.</summary>
    public CorporateSlotReservation ResolveSlotReservation(
        Guid membershipId,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
        => ResolveSlotReservation(
            membershipId,
            VehicleClass.FourWheeler,
            occupiedSharedSlotNumbers,
            sharedSlotUsageBySlot,
            anonymousOccupiedSharedBookings);

    public CorporateSlotReservation ResolveSharedSlotReservation(
        VehicleClass vehicleClass,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
    {
        if (Status != AllocationStatus.Active)
        {
            throw new InvalidOperationException("Can only book against an active allocation.");
        }

        EnsureClassOffered(vehicleClass);

        var candidateSlots = GetSharedSlotNumbers(vehicleClass)
            .Except(occupiedSharedSlotNumbers ?? Array.Empty<int>())
            .ToList();
        if (candidateSlots.Count <= Math.Max(0, anonymousOccupiedSharedBookings))
        {
            throw new InvalidOperationException(
                $"No shared {vehicleClass} parking slots available for the requested time.");
        }

        var usageBySlot = sharedSlotUsageBySlot ?? new Dictionary<int, int>();
        var selectedSlot = candidateSlots
            .OrderBy(slot => usageBySlot.TryGetValue(slot, out var usage) ? usage : 0)
            .ThenBy(slot => slot)
            .Skip(Math.Max(0, anonymousOccupiedSharedBookings))
            .First();

        return CorporateSlotReservation.Shared(selectedSlot);
    }

    /// <summary>Legacy helper: FourWheeler pool.</summary>
    public CorporateSlotReservation ResolveSharedSlotReservation(
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings = 0)
        => ResolveSharedSlotReservation(
            VehicleClass.FourWheeler,
            occupiedSharedSlotNumbers,
            sharedSlotUsageBySlot,
            anonymousOccupiedSharedBookings);

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

    private void SetClassQuotas(Quota twoWheelerQuota, Quota fourWheelerQuota)
    {
        ArgumentNullException.ThrowIfNull(twoWheelerQuota);
        ArgumentNullException.ThrowIfNull(fourWheelerQuota);

        if (twoWheelerQuota.IsEmpty && fourWheelerQuota.IsEmpty)
        {
            throw new ArgumentException("At least one vehicle class pool must have capacity.");
        }

        TwoWheelerQuota = twoWheelerQuota;
        FourWheelerQuota = fourWheelerQuota;
        Quota = Quota.Combine(twoWheelerQuota, fourWheelerQuota);
    }

    private IEnumerable<int> GetSharedSlotNumbers(VehicleClass vehicleClass)
    {
        var pool = GetQuota(vehicleClass);
        if (pool.SharedSlots <= 0)
        {
            return Enumerable.Empty<int>();
        }

        return Enumerable.Range(pool.FixedSlots + 1, pool.SharedSlots);
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
