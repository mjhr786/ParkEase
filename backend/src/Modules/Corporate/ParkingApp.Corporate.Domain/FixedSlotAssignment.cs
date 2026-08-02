using System.Diagnostics.CodeAnalysis;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Corporate.Domain;

/// <summary>
/// Represents a fixed parking slot assigned to a specific company member
/// within a ParkingAllocation and vehicle class pool.
/// </summary>
public class FixedSlotAssignment : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid AllocationId { get; private set; }
    public Guid MembershipId { get; private set; }
    public VehicleClass VehicleClass { get; private set; } = VehicleClass.FourWheeler;
    public int SlotNumber { get; private set; }
    public DateTime AssignedAt { get; private set; }

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual ParkingAllocation Allocation { get; private set; } = null!;
    public virtual UserCompanyMembership Membership { get; private set; } = null!;

    // Required for EF Core materialization — no business logic.
    [ExcludeFromCodeCoverage]
    private FixedSlotAssignment()
    {
    }

    internal static FixedSlotAssignment Create(
        Guid companyId,
        Guid allocationId,
        Guid membershipId,
        VehicleClass vehicleClass,
        int slotNumber)
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

        if (vehicleClass is not (VehicleClass.TwoWheeler or VehicleClass.FourWheeler))
        {
            throw new ArgumentOutOfRangeException(nameof(vehicleClass), "Vehicle class must be TwoWheeler or FourWheeler.");
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
            VehicleClass = vehicleClass,
            SlotNumber = slotNumber,
            AssignedAt = DateTime.UtcNow
        };
    }
}
