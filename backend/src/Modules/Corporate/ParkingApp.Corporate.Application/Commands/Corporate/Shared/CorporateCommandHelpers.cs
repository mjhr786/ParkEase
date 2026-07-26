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

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public static string BuildLockKey(Guid companyId, Guid allocationId, DateTime startUtc) =>
        $"lock:corp-booking:{companyId}:{allocationId}:{startUtc:yyyyMMddHH}";
}
