namespace ParkingApp.Domain.Entities.Corporate;

/// <summary>
/// Represents a fixed parking slot assigned to a specific company member
/// within a ParkingAllocation.
/// </summary>
public class FixedSlotAssignment : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid AllocationId { get; private set; }
    public Guid MembershipId { get; private set; }
    public int SlotNumber { get; private set; }
    public DateTime AssignedAt { get; private set; }

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual ParkingAllocation Allocation { get; private set; } = null!;
    public virtual UserCompanyMembership Membership { get; private set; } = null!;

    // Required for EF Core
    private FixedSlotAssignment()
    {
    }

    internal static FixedSlotAssignment Create(Guid companyId, Guid allocationId, Guid membershipId, int slotNumber)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (allocationId == Guid.Empty)
        {
            throw new ArgumentException("Allocation ID is required.", nameof(allocationId));
        }

        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        }

        if (slotNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "Slot number must be positive.");
        }

        return new FixedSlotAssignment
        {
            CompanyId = companyId,
            AllocationId = allocationId,
            MembershipId = membershipId,
            SlotNumber = slotNumber,
            AssignedAt = DateTime.UtcNow
        };
    }
}
