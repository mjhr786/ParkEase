using ParkingApp.Domain.Shared;
namespace ParkingApp.Domain.Corporate;

/// <summary>
/// Tracks daily usage per company allocation for billing purposes.
/// Each row represents one day of usage at one allocation.
/// Unique constraint: (CompanyId, AllocationId, UsageDate).
/// </summary>
public class CompanyUsage : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public Guid AllocationId { get; private set; }
    public DateOnly UsageDate { get; private set; }
    public int BookingCount { get; private set; }
    public int VisitorBookingCount { get; private set; }
    public decimal TotalHoursUsed { get; private set; }

    // Navigation
    public virtual Company Company { get; private set; } = null!;
    public virtual ParkingAllocation Allocation { get; private set; } = null!;

    // Required for EF Core
    private CompanyUsage()
    {
    }

    public static CompanyUsage Create(Guid companyId, Guid allocationId, DateOnly usageDate)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID is required.", nameof(companyId));
        }

        if (allocationId == Guid.Empty)
        {
            throw new ArgumentException("Allocation ID is required.", nameof(allocationId));
        }

        return new CompanyUsage
        {
            CompanyId = companyId,
            AllocationId = allocationId,
            UsageDate = usageDate,
            BookingCount = 0,
            VisitorBookingCount = 0,
            TotalHoursUsed = 0
        };
    }

    public void IncrementBooking(decimal hours, bool isVisitor)
    {
        if (hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours cannot be negative.");
        }

        BookingCount++;
        TotalHoursUsed += Math.Round(hours, 2, MidpointRounding.AwayFromZero);

        if (isVisitor)
        {
            VisitorBookingCount++;
        }
    }
}
