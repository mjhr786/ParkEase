using System.Diagnostics.CodeAnalysis;

namespace ParkingApp.Domain.ValueObjects;

/// <summary>
/// Represents the slot allocation quota for a corporate parking pool (or combined total).
/// Invariant: FixedSlots + SharedSlots cannot exceed TotalSlots.
/// Zero-total pools are allowed via <see cref="None"/> / <see cref="CreatePool"/> for dual-class allocations.
/// </summary>
public sealed record Quota
{
    public int TotalSlots { get; private init; }
    public int FixedSlots { get; private init; }
    public int SharedSlots { get; private init; }

    /// <summary>Empty pool (class not offered).</summary>
    public static Quota None { get; } = new(0, 0, 0);

    // Required for EF Core materialization — no business logic.
    [ExcludeFromCodeCoverage]
    private Quota()
    {
    }

    private Quota(int totalSlots, int fixedSlots, int sharedSlots)
    {
        if (totalSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSlots), "Total slots cannot be negative.");
        }

        if (fixedSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedSlots), "Fixed slots cannot be negative.");
        }

        if (sharedSlots < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sharedSlots), "Shared slots cannot be negative.");
        }

        if (totalSlots == 0)
        {
            if (fixedSlots != 0 || sharedSlots != 0)
            {
                throw new ArgumentException("A zero-capacity pool cannot have fixed or shared slots.");
            }
        }
        else if (fixedSlots + sharedSlots > totalSlots)
        {
            throw new ArgumentException("Fixed slots plus shared slots cannot exceed total slots.");
        }

        TotalSlots = totalSlots;
        FixedSlots = fixedSlots;
        SharedSlots = sharedSlots;
    }

    /// <summary>
    /// Creates a positive-capacity quota (total must be greater than zero).
    /// Use <see cref="CreatePool"/> when zero is allowed.
    /// </summary>
    public static Quota Create(int totalSlots, int fixedSlots, int sharedSlots)
    {
        if (totalSlots <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSlots), "Total slots must be greater than zero.");
        }

        return new Quota(totalSlots, fixedSlots, sharedSlots);
    }

    /// <summary>
    /// Creates a class pool; total may be zero (class not offered).
    /// </summary>
    public static Quota CreatePool(int totalSlots, int fixedSlots, int sharedSlots)
        => new(totalSlots, fixedSlots, sharedSlots);

    /// <summary>
    /// Combined mirror of two class pools (for legacy TotalSlots columns / reporting).
    /// </summary>
    public static Quota Combine(Quota twoWheeler, Quota fourWheeler)
    {
        ArgumentNullException.ThrowIfNull(twoWheeler);
        ArgumentNullException.ThrowIfNull(fourWheeler);

        var total = twoWheeler.TotalSlots + fourWheeler.TotalSlots;
        if (total <= 0)
        {
            throw new ArgumentException("At least one vehicle class pool must have capacity.");
        }

        return new Quota(
            total,
            twoWheeler.FixedSlots + fourWheeler.FixedSlots,
            twoWheeler.SharedSlots + fourWheeler.SharedSlots);
    }

    public bool HasFixedSlots => FixedSlots > 0;
    public bool HasSharedSlots => SharedSlots > 0;
    public bool IsEmpty => TotalSlots == 0;
    public int UnallocatedSlots => TotalSlots - FixedSlots - SharedSlots;
}
