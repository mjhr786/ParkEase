using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Shared;

internal static class CorporateCommandHelpers
{
    public static BookingPolicy? CreateBookingPolicy(BookingPolicyDto? dto)
    {
        if (dto is null)
            return null;

        return BookingPolicy.Create(
            dto.MaxBookingsPerEmployeePerDay,
            dto.MaxBookingsPerEmployeePerWeek,
            dto.PriorityThreshold,
            dto.AllowedStartTime ?? new TimeSpan(7, 0, 0),
            dto.AllowedEndTime ?? new TimeSpan(22, 0, 0),
            dto.AllowWeekends);
    }

    /// <summary>
    /// Resolves dual class pools from request body.
    /// Prefer nested TwoWheeler/FourWheeler; otherwise legacy Total/Fixed/Shared → FourWheeler only.
    /// </summary>
    public static (Quota TwoWheeler, Quota FourWheeler) ResolveClassQuotas(
        SlotPoolDto? twoWheeler,
        SlotPoolDto? fourWheeler,
        int legacyTotalSlots,
        int legacyFixedSlots,
        int legacySharedSlots)
    {
        var hasClassPools = twoWheeler is not null || fourWheeler is not null;
        if (hasClassPools)
        {
            return (
                ToPool(twoWheeler),
                ToPool(fourWheeler));
        }

        if (legacyTotalSlots <= 0)
        {
            throw new ArgumentException(
                "Provide TwoWheeler/FourWheeler pools, or legacy totalSlots greater than zero.");
        }

        return (
            Quota.None,
            Quota.Create(legacyTotalSlots, legacyFixedSlots, legacySharedSlots));
    }

    private static Quota ToPool(SlotPoolDto? dto)
    {
        if (dto is null)
            return Quota.None;

        return Quota.CreatePool(dto.TotalSlots, dto.FixedSlots, dto.SharedSlots);
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public static string BuildLockKey(Guid companyId, Guid allocationId, DateTime startUtc) =>
        $"lock:corp-booking:{companyId}:{allocationId}:{startUtc:yyyyMMddHH}";
}
