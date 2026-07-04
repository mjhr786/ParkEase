namespace ParkingApp.Domain.ValueObjects;

/// <summary>
/// Represents the slot allocation quota for a corporate parking contract.
/// Invariant: FixedSlots + SharedSlots must equal TotalSlots.
/// </summary>
public sealed record Quota
{
    public int TotalSlots { get; private init; }
    public int FixedSlots { get; private init; }
    public int SharedSlots { get; private init; }

    private Quota()
    {
    }

    private Quota(int totalSlots, int fixedSlots, int sharedSlots)
    {
        if (totalSlots <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSlots), "Total slots must be greater than zero.");
        }

        if (fixedSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedSlots), "Fixed slots cannot be negative.");
        }

        if (sharedSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sharedSlots), "Shared slots cannot be negative.");
        }

        if (fixedSlots + sharedSlots > totalSlots)
        {
            throw new ArgumentException("Fixed slots plus shared slots cannot exceed total slots.");
        }

        TotalSlots = totalSlots;
        FixedSlots = fixedSlots;
        SharedSlots = sharedSlots;
    }

    public static Quota Create(int totalSlots, int fixedSlots, int sharedSlots)
        => new(totalSlots, fixedSlots, sharedSlots);

    public bool HasFixedSlots => FixedSlots > 0;
    public bool HasSharedSlots => SharedSlots > 0;
    public int UnallocatedSlots => TotalSlots - FixedSlots - SharedSlots;
}
