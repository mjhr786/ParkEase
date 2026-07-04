using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Entities.Corporate;

/// <summary>
/// Company Aggregate Root.
/// Central B2B entity that owns memberships, allocations, and invitations.
/// All business rules for company management are enforced here.
/// </summary>
public class Company : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string RegistrationNumber { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public string BillingAddress { get; private set; } = string.Empty;
    public BillingType BillingType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid CreatedByUserId { get; private set; }

    // Navigation
    public virtual User CreatedByUser { get; private set; } = null!;
    public virtual ICollection<UserCompanyMembership> Memberships { get; private set; } = new List<UserCompanyMembership>();
    public virtual ICollection<ParkingAllocation> Allocations { get; private set; } = new List<ParkingAllocation>();
    public virtual ICollection<EmployeeInvitation> Invitations { get; private set; } = new List<EmployeeInvitation>();
    public virtual ICollection<CorporateBooking> CorporateBookings { get; private set; } = new List<CorporateBooking>();
    public virtual ICollection<CompanyUsage> Usages { get; private set; } = new List<CompanyUsage>();
    public virtual ICollection<CorporateWaitlistEntry> WaitlistEntries { get; private set; } = new List<CorporateWaitlistEntry>();

    // Required for EF Core
    private Company()
    {
    }

    private Company(
        string name,
        string registrationNumber,
        string contactEmail,
        string contactPhone,
        string billingAddress,
        BillingType billingType,
        Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            throw new ArgumentException("Registration number is required.", nameof(registrationNumber));
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new ArgumentException("Contact email is required.", nameof(contactEmail));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user ID is required.", nameof(createdByUserId));
        }

        Name = name.Trim();
        RegistrationNumber = registrationNumber.Trim().ToUpperInvariant();
        ContactEmail = contactEmail.Trim().ToLowerInvariant();
        ContactPhone = contactPhone?.Trim() ?? string.Empty;
        BillingAddress = billingAddress?.Trim() ?? string.Empty;
        BillingType = billingType;
        CreatedByUserId = createdByUserId;
    }

    public static Company Create(
        string name,
        string registrationNumber,
        string contactEmail,
        string contactPhone,
        string billingAddress,
        BillingType billingType,
        Guid createdByUserId)
    {
        var company = new Company(name, registrationNumber, contactEmail, contactPhone, billingAddress, billingType, createdByUserId);
        company.AddMembershipInternal(createdByUserId, CompanyRole.Admin);

        return company;
    }

    public UserCompanyMembership AddMember(Guid adminUserId, Guid userId, CompanyRole role, string? employeeCode = null, int priority = 1)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);

        return AddMembershipInternal(userId, role, employeeCode, priority);
    }

    public EmployeeInvitation InviteMember(Guid adminUserId, string email, CompanyRole role, bool emailAlreadyBelongsToMember = false)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);

        var normalizedEmail = NormalizeEmail(email);

        if (emailAlreadyBelongsToMember
            || Memberships.Any(m => !m.IsDeleted && string.Equals(m.User?.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("This user is already a member of the company.");
        }

        if (Invitations.Any(i => !i.IsDeleted && i.IsPending && string.Equals(i.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("There is already a pending invitation for this email address.");
        }

        var invitation = EmployeeInvitation.Create(Id, normalizedEmail, role, adminUserId);
        Invitations.Add(invitation);

        return invitation;
    }

    public UserCompanyMembership AcceptInvitation(
        string invitationToken,
        Guid userId,
        string userEmail,
        string? employeeCode = null,
        int priority = 1)
    {
        EnsureIsActive();

        var invitation = Invitations.FirstOrDefault(i =>
            !i.IsDeleted &&
            string.Equals(i.InvitationToken, invitationToken?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (invitation == null)
        {
            throw new InvalidOperationException("Invalid or expired invitation.");
        }

        var normalizedEmail = NormalizeEmail(userEmail);
        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This invitation was sent to a different email address.");
        }

        if (Memberships.Any(m => m.UserId == userId && !m.IsDeleted))
        {
            throw new InvalidOperationException("User is already a member of this company.");
        }

        invitation.Accept(userId);
        return AddMembershipInternal(userId, invitation.Role, employeeCode, priority);
    }

    public void RemoveMember(Guid adminUserId, Guid membershipId)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);
        RemoveMembershipInternal(membershipId);
    }

    public ParkingAllocation RequestAllocation(
        Guid adminUserId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        int parkingCapacity,
        BookingPolicy? bookingPolicy = null)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);

        if (quota == null)
        {
            throw new ArgumentNullException(nameof(quota));
        }

        if (quota.TotalSlots > parkingCapacity)
        {
            throw new InvalidOperationException($"Cannot allocate more than {parkingCapacity} total spots available.");
        }

        var allocation = ParkingAllocation.Create(Id, parkingSpaceId, quota, monthlyRate, startDate, endDate, bookingPolicy);
        Allocations.Add(allocation);

        return allocation;
    }

    public ParkingAllocation CreateOwnedParkingAllocation(
        Guid adminUserId,
        Guid parkingSpaceId,
        Quota quota,
        decimal monthlyRate,
        DateTime startDate,
        DateTime endDate,
        int parkingCapacity,
        BookingPolicy? bookingPolicy = null)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);

        if (quota == null)
        {
            throw new ArgumentNullException(nameof(quota));
        }

        if (quota.TotalSlots > parkingCapacity)
        {
            throw new InvalidOperationException($"Cannot allocate more than {parkingCapacity} total spots available.");
        }

        var allocation = ParkingAllocation.CreateCompanyOwned(
            Id,
            parkingSpaceId,
            quota,
            monthlyRate,
            startDate,
            endDate,
            adminUserId,
            bookingPolicy);

        Allocations.Add(allocation);
        return allocation;
    }

    public void ApproveAllocation(Guid allocationId, Guid approvedByUserId)
    {
        RequireAllocation(allocationId).Approve(approvedByUserId);
    }

    public void RejectAllocation(Guid allocationId, string? reason)
    {
        RequireAllocation(allocationId).Reject(reason);
    }

    public void UpdateAllocationPolicy(Guid adminUserId, Guid allocationId, BookingPolicy policy)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);
        RequireAllocation(allocationId).UpdateBookingPolicy(policy);
    }

    public void AssignFixedSlot(Guid adminUserId, Guid allocationId, Guid membershipId, int slotNumber)
    {
        EnsureIsActive();
        RequireAdminMembership(adminUserId);

        var member = RequireMembershipById(membershipId, requireActive: true);
        var allocation = RequireAllocation(allocationId, requireActive: true);

        allocation.AssignFixedSlot(member, slotNumber);
    }

    public CorporateFraudAssessment AssessFraudRisk(
        Guid userId,
        DateTime bookingStart,
        DateTime bookingEnd,
        bool hasOverlappingMemberBooking,
        bool hasOverlappingVehicleBooking,
        int recentBookingCreations)
    {
        EnsureIsActive();
        RequireActiveMembership(userId);

        if (bookingEnd <= bookingStart)
        {
            throw new ArgumentException("Booking end time must be after the start time.");
        }

        if (hasOverlappingMemberBooking)
        {
            return CorporateFraudAssessment.Block(
                CorporateFraudRiskLevel.High,
                "Suspicious duplicate booking detected. You already have an overlapping corporate booking.");
        }

        if (hasOverlappingVehicleBooking)
        {
            return CorporateFraudAssessment.Block(
                CorporateFraudRiskLevel.High,
                "Suspicious vehicle reuse detected. This vehicle already has an overlapping corporate booking.");
        }

        if (recentBookingCreations >= 6)
        {
            return CorporateFraudAssessment.Flag(
                CorporateFraudRiskLevel.Medium,
                "Unusually high corporate booking activity detected for this member.");
        }

        if (recentBookingCreations >= 3)
        {
            return CorporateFraudAssessment.Flag(
                CorporateFraudRiskLevel.Low,
                "Elevated booking frequency detected for this member.");
        }

        return CorporateFraudAssessment.None();
    }

    public CorporateReservationOutcome ReserveEmployeeParking(
        Guid userId,
        Guid allocationId,
        Booking booking,
        int currentDayBookings,
        int currentWeekBookings,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings,
        CorporateFraudAssessment fraudAssessment)
    {
        EnsureIsActive();

        var membership = RequireActiveMembership(userId);
        var allocation = RequireAllocation(allocationId, requireActive: true);

        ValidateBookingTarget(booking, allocation);
        allocation.EnsureEmployeeBookingAllowed(
            membership.Priority,
            booking.StartDateTime,
            booking.EndDateTime,
            currentDayBookings,
            currentWeekBookings);

        EnsureFraudAssessmentAllowed(fraudAssessment);

        CorporateWaitlistEntry? waitlistEntry = null;
        if (!allocation.HasFixedSlotAssignment(membership.Id))
        {
            waitlistEntry = FindPendingEmployeeWaitlist(membership.Id, allocation.Id, booking.StartDateTime, booking.EndDateTime, booking.VehicleNumber);
            var queueHead = GetPendingWaitlistHead(allocation.Id, booking.StartDateTime, booking.EndDateTime);

            var canAllocateSharedSlot = allocation.GetAvailableSharedSlots(occupiedSharedSlotNumbers, anonymousOccupiedSharedBookings) > 0;
            var queueBlocksRequester = queueHead != null && (waitlistEntry == null || queueHead.Id != waitlistEntry.Id);
            if (!canAllocateSharedSlot || queueBlocksRequester)
            {
                waitlistEntry ??= AddEmployeeWaitlistEntry(membership, allocation.Id, booking);
                return new CorporateReservationOutcome(null, waitlistEntry, fraudAssessment);
            }
        }

        var slotReservation = allocation.ResolveSlotReservation(
            membership.Id,
            occupiedSharedSlotNumbers,
            sharedSlotUsageBySlot,
            anonymousOccupiedSharedBookings);
        ConfirmCorporateBooking(booking, slotReservation);

        var corporateBooking = CorporateBooking.CreateEmployeeBooking(
            Id,
            membership.Id,
            allocation.Id,
            booking.Id,
            slotReservation.SlotType);

        CorporateBookings.Add(corporateBooking);
        waitlistEntry?.Promote(booking.Id);
        RecordUsage(allocation.Id, booking.StartDateTime, booking.Duration.TotalHours, isVisitor: false);

        return new CorporateReservationOutcome(corporateBooking, null, fraudAssessment);
    }

    public CorporateReservationOutcome ReserveVisitorParking(
        Guid userId,
        Guid allocationId,
        Booking booking,
        string visitorName,
        string visitorLicensePlate,
        DateTime accessExpiry,
        IReadOnlyCollection<int> occupiedSharedSlotNumbers,
        IReadOnlyDictionary<int, int> sharedSlotUsageBySlot,
        int anonymousOccupiedSharedBookings,
        CorporateFraudAssessment fraudAssessment)
    {
        EnsureIsActive();

        var membership = RequireActiveMembership(userId);
        var allocation = RequireAllocation(allocationId, requireActive: true);

        ValidateBookingTarget(booking, allocation);
        allocation.EnsureVisitorBookingAllowed(booking.StartDateTime, booking.EndDateTime);

        EnsureFraudAssessmentAllowed(fraudAssessment);

        var waitlistEntry = FindPendingVisitorWaitlist(membership.Id, allocation.Id, booking.StartDateTime, booking.EndDateTime, visitorLicensePlate);
        var queueHead = GetPendingWaitlistHead(allocation.Id, booking.StartDateTime, booking.EndDateTime);
        var canAllocateSharedSlot = allocation.GetAvailableSharedSlots(occupiedSharedSlotNumbers, anonymousOccupiedSharedBookings) > 0;
        var queueBlocksRequester = queueHead != null && (waitlistEntry == null || queueHead.Id != waitlistEntry.Id);
        if (!canAllocateSharedSlot || queueBlocksRequester)
        {
            waitlistEntry ??= AddVisitorWaitlistEntry(membership, allocation.Id, booking, visitorName, visitorLicensePlate, accessExpiry);
            return new CorporateReservationOutcome(null, waitlistEntry, fraudAssessment);
        }

        var slotReservation = allocation.ResolveSharedSlotReservation(
            occupiedSharedSlotNumbers,
            sharedSlotUsageBySlot,
            anonymousOccupiedSharedBookings);
        ConfirmCorporateBooking(booking, slotReservation);

        var accessPolicy = AccessPolicy.Create(visitorLicensePlate, booking.StartDateTime, accessExpiry);
        var corporateBooking = CorporateBooking.CreateVisitorBooking(
            Id,
            membership.Id,
            allocation.Id,
            booking.Id,
            visitorName,
            visitorLicensePlate,
            accessPolicy);

        CorporateBookings.Add(corporateBooking);
        waitlistEntry?.Promote(booking.Id);
        RecordUsage(allocation.Id, booking.StartDateTime, booking.Duration.TotalHours, isVisitor: true);

        return new CorporateReservationOutcome(corporateBooking, null, fraudAssessment);
    }

    public decimal CalculateBookingAmount(decimal hourlyRate, TimeSpan duration)
    {
        if (hourlyRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyRate), "Hourly rate cannot be negative.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Booking duration must be greater than zero.");
        }

        if (BillingType == BillingType.ReservedSlots)
        {
            return 0m;
        }

        var billableHours = (decimal)Math.Ceiling(duration.TotalHours);
        return Math.Round(hourlyRate * billableHours, 2, MidpointRounding.AwayFromZero);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void UpdateBillingType(BillingType type)
    {
        BillingType = type;
    }

    public void UpdateDetails(string? name, string? contactEmail, string? contactPhone, string? billingAddress)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            ContactEmail = contactEmail.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(contactPhone))
        {
            ContactPhone = contactPhone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(billingAddress))
        {
            BillingAddress = billingAddress.Trim();
        }
    }

    public int GetWaitlistPosition(Guid waitlistEntryId)
    {
        var targetEntry = WaitlistEntries.FirstOrDefault(w => w.Id == waitlistEntryId && !w.IsDeleted && w.Status == WaitlistStatus.Pending);
        if (targetEntry == null)
        {
            throw new InvalidOperationException("Waitlist entry not found.");
        }

        return GetPendingWaitlistEntries(targetEntry.AllocationId, targetEntry.RequestedStartDateTime, targetEntry.RequestedEndDateTime)
            .ToList()
            .FindIndex(w => w.Id == waitlistEntryId) + 1;
    }

    public void CancelWaitlistEntry(Guid userId, Guid waitlistEntryId)
    {
        EnsureIsActive();
        var membership = RequireActiveMembership(userId);

        var waitlistEntry = WaitlistEntries.FirstOrDefault(w => w.Id == waitlistEntryId && !w.IsDeleted);
        if (waitlistEntry == null)
        {
            throw new InvalidOperationException("Waitlist entry not found.");
        }

        if (waitlistEntry.MembershipId != membership.Id && !membership.IsAdmin)
        {
            throw new InvalidOperationException("Only the requester or a company admin can cancel this waitlist entry.");
        }

        waitlistEntry.Cancel();
    }

    private UserCompanyMembership AddMembershipInternal(Guid userId, CompanyRole role, string? employeeCode = null, int priority = 1)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (Memberships.Any(m => m.UserId == userId && !m.IsDeleted))
        {
            throw new InvalidOperationException("User is already a member of this company.");
        }

        var membership = UserCompanyMembership.Create(Id, userId, role, employeeCode, priority);
        Memberships.Add(membership);

        return membership;
    }

    private void RemoveMembershipInternal(Guid membershipId)
    {
        var membership = Memberships.FirstOrDefault(m => m.Id == membershipId && !m.IsDeleted);
        if (membership == null)
        {
            throw new InvalidOperationException("Membership not found.");
        }

        if (membership.Role == CompanyRole.Admin)
        {
            var adminCount = Memberships.Count(m => m.Role == CompanyRole.Admin && !m.IsDeleted && m.Id != membershipId);
            if (adminCount == 0)
            {
                throw new InvalidOperationException("Cannot remove the last admin of the company.");
            }
        }

        membership.Deactivate();
    }

    private UserCompanyMembership RequireAdminMembership(Guid userId)
    {
        var membership = RequireActiveMembership(userId);
        if (!membership.IsAdmin)
        {
            throw new InvalidOperationException("Only company admins can perform this action.");
        }

        return membership;
    }

    private UserCompanyMembership RequireActiveMembership(Guid userId)
    {
        var membership = Memberships.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted);
        if (membership == null || !membership.IsActive)
        {
            throw new InvalidOperationException("You are not an active member of this company.");
        }

        return membership;
    }

    private UserCompanyMembership RequireMembershipById(Guid membershipId, bool requireActive)
    {
        var membership = Memberships.FirstOrDefault(m => m.Id == membershipId && !m.IsDeleted);
        if (membership == null)
        {
            throw new InvalidOperationException("Membership not found.");
        }

        if (requireActive && !membership.IsActive)
        {
            throw new InvalidOperationException("Target member is not active in this company.");
        }

        return membership;
    }

    private ParkingAllocation RequireAllocation(Guid allocationId, bool requireActive = false)
    {
        var allocation = Allocations.FirstOrDefault(a => a.Id == allocationId && !a.IsDeleted);
        if (allocation == null)
        {
            throw new InvalidOperationException("Allocation not found.");
        }

        if (requireActive && !allocation.IsActiveAllocation)
        {
            throw new InvalidOperationException("Active allocation not found.");
        }

        return allocation;
    }

    private void ValidateBookingTarget(Booking booking, ParkingAllocation allocation)
    {
        if (booking == null)
        {
            throw new ArgumentNullException(nameof(booking));
        }

        if (booking.ParkingSpaceId != allocation.ParkingSpaceId)
        {
            throw new InvalidOperationException("Booking parking space does not match the selected company allocation.");
        }

        if (booking.EndDateTime <= booking.StartDateTime)
        {
            throw new ArgumentException("Booking end time must be after the start time.", nameof(booking));
        }
    }

    private void ConfirmCorporateBooking(Booking booking, CorporateSlotReservation slotReservation)
    {
        if (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.AwaitingPayment)
        {
            booking.Confirm();
        }
        else if (booking.Status != BookingStatus.Confirmed)
        {
            throw new InvalidOperationException($"Corporate bookings must be confirmed before registration. Current status: {booking.Status}.");
        }

        booking.SlotNumber = slotReservation.SlotNumber;
    }

    private void EnsureFraudAssessmentAllowed(CorporateFraudAssessment fraudAssessment)
    {
        if (fraudAssessment == null)
        {
            throw new ArgumentNullException(nameof(fraudAssessment));
        }

        if (fraudAssessment.IsBlocked)
        {
            throw new InvalidOperationException(fraudAssessment.Reason ?? "Suspicious booking activity detected.");
        }
    }

    private CorporateWaitlistEntry AddEmployeeWaitlistEntry(UserCompanyMembership membership, Guid allocationId, Booking booking)
    {
        var waitlistEntry = CorporateWaitlistEntry.CreateEmployee(
            Id,
            membership.Id,
            allocationId,
            booking.StartDateTime,
            booking.EndDateTime,
            booking.VehicleType,
            booking.VehicleNumber,
            membership.Priority);

        WaitlistEntries.Add(waitlistEntry);
        return waitlistEntry;
    }

    private CorporateWaitlistEntry AddVisitorWaitlistEntry(
        UserCompanyMembership membership,
        Guid allocationId,
        Booking booking,
        string visitorName,
        string visitorLicensePlate,
        DateTime accessExpiry)
    {
        var waitlistEntry = CorporateWaitlistEntry.CreateVisitor(
            Id,
            membership.Id,
            allocationId,
            booking.StartDateTime,
            booking.EndDateTime,
            visitorName,
            visitorLicensePlate,
            accessExpiry,
            membership.Priority);

        WaitlistEntries.Add(waitlistEntry);
        return waitlistEntry;
    }

    private CorporateWaitlistEntry? FindPendingEmployeeWaitlist(Guid membershipId, Guid allocationId, DateTime startUtc, DateTime endUtc, string? vehicleNumber)
    {
        return WaitlistEntries.FirstOrDefault(w =>
            !w.IsDeleted &&
            w.Status == WaitlistStatus.Pending &&
            w.AllocationId == allocationId &&
            w.MatchesEmployeeRequest(membershipId, startUtc, endUtc, vehicleNumber));
    }

    private CorporateWaitlistEntry? FindPendingVisitorWaitlist(Guid membershipId, Guid allocationId, DateTime startUtc, DateTime endUtc, string visitorLicensePlate)
    {
        return WaitlistEntries.FirstOrDefault(w =>
            !w.IsDeleted &&
            w.Status == WaitlistStatus.Pending &&
            w.AllocationId == allocationId &&
            w.MatchesVisitorRequest(membershipId, startUtc, endUtc, visitorLicensePlate));
    }

    private CorporateWaitlistEntry? GetPendingWaitlistHead(Guid allocationId, DateTime startUtc, DateTime endUtc)
    {
        return GetPendingWaitlistEntries(allocationId, startUtc, endUtc).FirstOrDefault();
    }

    private IOrderedEnumerable<CorporateWaitlistEntry> GetPendingWaitlistEntries(Guid allocationId, DateTime startUtc, DateTime endUtc)
    {
        return WaitlistEntries
            .Where(w =>
                !w.IsDeleted &&
                w.Status == WaitlistStatus.Pending &&
                w.AllocationId == allocationId &&
                w.Overlaps(startUtc, endUtc))
            .OrderByDescending(w => w.PriorityAtRequest)
            .ThenBy(w => w.CreatedAt);
    }

    private void RecordUsage(Guid allocationId, DateTime bookingStart, double hours, bool isVisitor)
    {
        var usageDate = DateOnly.FromDateTime(bookingStart);
        var usage = Usages.FirstOrDefault(u =>
            !u.IsDeleted &&
            u.AllocationId == allocationId &&
            u.UsageDate == usageDate);

        if (usage == null)
        {
            usage = CompanyUsage.Create(Id, allocationId, usageDate);
            Usages.Add(usage);
        }

        usage.IncrementBooking((decimal)hours, isVisitor);
    }

    private void EnsureIsActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("This company is inactive.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        return email.Trim().ToLowerInvariant();
    }
}
