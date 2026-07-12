using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Marketplace;

public class ParkingPass : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid? ParkingSpaceId { get; private set; }
    public string? ParkingZoneCode { get; private set; }
    public Guid? AllocatedByUserId { get; private set; }
    public string? CorporateBatchReference { get; private set; }
    public PassCoverageType CoverageType { get; private set; }
    public PassType PassType { get; private set; } = PassType.Monthly();
    public Duration Duration { get; private set; } = Duration.Create(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
    public UsagePolicy UsagePolicy { get; private set; } = UsagePolicy.UnlimitedEntries();
    public decimal DiscountPercentage { get; private set; }

    public virtual User User { get; private set; } = null!;
    public virtual User? AllocatedByUser { get; private set; }
    public virtual ParkingSpace? ParkingSpace { get; private set; }
    public virtual ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    public ParkingPass()
    {
    }

    private ParkingPass(
        Guid userId,
        PassType passType,
        Duration duration,
        UsagePolicy usagePolicy,
        decimal discountPercentage,
        Guid? parkingSpaceId,
        string? parkingZoneCode,
        Guid? allocatedByUserId,
        string? corporateBatchReference)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A parking pass must be assigned to a valid user.", nameof(userId));
        }

        ValidateCoverage(parkingSpaceId, parkingZoneCode);

        if (discountPercentage < 0 || discountPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(discountPercentage), "Discount percentage must be between 0 and 100.");
        }

        UserId = userId;
        PassType = passType ?? throw new ArgumentNullException(nameof(passType));
        Duration = duration ?? throw new ArgumentNullException(nameof(duration));
        UsagePolicy = usagePolicy ?? throw new ArgumentNullException(nameof(usagePolicy));
        DiscountPercentage = Math.Round(discountPercentage, 2, MidpointRounding.AwayFromZero);
        ParkingSpaceId = parkingSpaceId;
        ParkingZoneCode = parkingZoneCode?.Trim().ToUpperInvariant();
        AllocatedByUserId = allocatedByUserId;
        CorporateBatchReference = string.IsNullOrWhiteSpace(corporateBatchReference) ? null : corporateBatchReference.Trim().ToUpperInvariant();
        CoverageType = parkingSpaceId.HasValue ? PassCoverageType.ParkingSpace : PassCoverageType.ParkingZone;
    }

    public static ParkingPass Create(
        Guid userId,
        PassType passType,
        Duration duration,
        UsagePolicy usagePolicy,
        decimal discountPercentage,
        Guid? parkingSpaceId,
        string? parkingZoneCode,
        Guid? allocatedByUserId = null,
        string? corporateBatchReference = null)
    {
        return new ParkingPass(
            userId,
            passType,
            duration,
            usagePolicy,
            discountPercentage,
            parkingSpaceId,
            parkingZoneCode,
            allocatedByUserId,
            corporateBatchReference);
    }

    public bool IsActiveOn(DateTime utcNow) => Duration.IsActiveOn(utcNow);

    public bool IsExpiredOn(DateTime utcNow) => Duration.IsExpiredOn(utcNow);

    public bool IsValidForBooking(
        ParkingSpace parkingSpace,
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        IReadOnlyDictionary<DateOnly, decimal> existingBookedHoursByDay,
        DateTime utcNow)
    {
        return IsActiveOn(utcNow)
            && Duration.Covers(bookingStartUtc, bookingEndUtc)
            && Covers(parkingSpace)
            && UsagePolicy.AllowsBooking(bookingStartUtc, bookingEndUtc, existingBookedHoursByDay);
    }

    public bool Covers(ParkingSpace parkingSpace)
    {
        if (parkingSpace == null)
        {
            throw new ArgumentNullException(nameof(parkingSpace));
        }

        return CoverageType switch
        {
            PassCoverageType.ParkingSpace => ParkingSpaceId == parkingSpace.Id,
            PassCoverageType.ParkingZone => !string.IsNullOrWhiteSpace(ParkingZoneCode)
                && string.Equals(ParkingZoneCode, parkingSpace.ZoneCode?.Trim(), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public decimal CalculateDiscountAmount(decimal grossAmount)
    {
        if (grossAmount <= 0 || DiscountPercentage <= 0)
        {
            return 0;
        }

        return Math.Round(grossAmount * (DiscountPercentage / 100m), 2, MidpointRounding.AwayFromZero);
    }

    public string GetState(DateTime utcNow)
    {
        if (IsExpiredOn(utcNow))
        {
            return "Expired";
        }

        if (IsActiveOn(utcNow))
        {
            return "Active";
        }

        return "Scheduled";
    }

    private static void ValidateCoverage(Guid? parkingSpaceId, string? parkingZoneCode)
    {
        var hasParkingSpace = parkingSpaceId.HasValue && parkingSpaceId.Value != Guid.Empty;
        var hasParkingZone = !string.IsNullOrWhiteSpace(parkingZoneCode);

        if (hasParkingSpace == hasParkingZone)
        {
            throw new ArgumentException("A parking pass must target exactly one scope: either a parking space or a parking zone.");
        }
    }
}
