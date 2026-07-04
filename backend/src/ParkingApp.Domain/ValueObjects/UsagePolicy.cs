using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.ValueObjects;

public sealed record UsagePolicy
{
    public PassUsageMode Mode { get; private init; }
    public int? DailyHourLimit { get; private init; }

    private UsagePolicy()
    {
    }

    private UsagePolicy(PassUsageMode mode, int? dailyHourLimit)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported usage policy.");
        }

        if (mode == PassUsageMode.UnlimitedEntries && dailyHourLimit.HasValue)
        {
            throw new ArgumentException("Unlimited entry passes cannot define a daily hour limit.");
        }

        if (mode == PassUsageMode.LimitedHoursPerDay && (!dailyHourLimit.HasValue || dailyHourLimit.Value <= 0 || dailyHourLimit.Value > 24))
        {
            throw new ArgumentException("Limited-hour passes must define a daily hour limit between 1 and 24.");
        }

        Mode = mode;
        DailyHourLimit = dailyHourLimit;
    }

    public static UsagePolicy UnlimitedEntries() => new(PassUsageMode.UnlimitedEntries, null);
    public static UsagePolicy LimitedHoursPerDay(int dailyHourLimit) => new(PassUsageMode.LimitedHoursPerDay, dailyHourLimit);

    public bool IsUnlimitedEntries => Mode == PassUsageMode.UnlimitedEntries;

    public bool AllowsBooking(
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        IReadOnlyDictionary<DateOnly, decimal> existingBookedHoursByDay)
    {
        if (IsUnlimitedEntries)
        {
            return true;
        }

        foreach (var requestedHours in GetRequestedHoursByDay(bookingStartUtc, bookingEndUtc))
        {
            existingBookedHoursByDay.TryGetValue(requestedHours.Key, out var existingHours);
            if (existingHours + requestedHours.Value > DailyHourLimit!.Value)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyDictionary<DateOnly, decimal> GetRequestedHoursByDay(DateTime bookingStartUtc, DateTime bookingEndUtc)
    {
        var startUtc = Normalize(bookingStartUtc);
        var endUtc = Normalize(bookingEndUtc);
        if (endUtc <= startUtc)
        {
            return new Dictionary<DateOnly, decimal>();
        }

        var hoursByDay = new Dictionary<DateOnly, decimal>();
        var cursor = startUtc;

        while (cursor < endUtc)
        {
            var nextBoundary = cursor.Date.AddDays(1);
            var segmentEnd = nextBoundary < endUtc ? nextBoundary : endUtc;
            var day = DateOnly.FromDateTime(cursor);
            var hours = Math.Round((decimal)(segmentEnd - cursor).TotalHours, 2, MidpointRounding.AwayFromZero);

            if (hoursByDay.TryGetValue(day, out var currentHours))
            {
                hoursByDay[day] = currentHours + hours;
            }
            else
            {
                hoursByDay[day] = hours;
            }

            cursor = segmentEnd;
        }

        return hoursByDay;
    }

    private static DateTime Normalize(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
