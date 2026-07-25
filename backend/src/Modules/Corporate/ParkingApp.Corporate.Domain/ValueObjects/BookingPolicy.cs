namespace ParkingApp.Domain.ValueObjects;

/// <summary>
/// Corporate booking policy that defines rules for a specific parking allocation.
/// Owned by ParkingAllocation (not Company) to allow per-location rules.
/// </summary>
public sealed record BookingPolicy
{
    public int MaxBookingsPerEmployeePerDay { get; private init; } = 1;
    public int MaxBookingsPerEmployeePerWeek { get; private init; } = 5;
    public int PriorityThreshold { get; private init; } = 1;
    public TimeSpan AllowedStartTime { get; private init; } = new(7, 0, 0);
    public TimeSpan AllowedEndTime { get; private init; } = new(22, 0, 0);
    public bool AllowWeekends { get; private init; }

    private BookingPolicy()
    {
    }

    private BookingPolicy(
        int maxBookingsPerDay,
        int maxBookingsPerWeek,
        int priorityThreshold,
        TimeSpan allowedStartTime,
        TimeSpan allowedEndTime,
        bool allowWeekends)
    {
        if (maxBookingsPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBookingsPerDay), "Max bookings per day must be at least 1.");
        }

        if (maxBookingsPerWeek <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBookingsPerWeek), "Max bookings per week must be at least 1.");
        }

        if (maxBookingsPerWeek < maxBookingsPerDay)
        {
            throw new ArgumentException("Weekly limit cannot be less than daily limit.");
        }

        if (priorityThreshold < 1 || priorityThreshold > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(priorityThreshold), "Priority threshold must be between 1 and 10.");
        }

        if (allowedEndTime <= allowedStartTime)
        {
            throw new ArgumentException("Allowed end time must be after start time.");
        }

        MaxBookingsPerEmployeePerDay = maxBookingsPerDay;
        MaxBookingsPerEmployeePerWeek = maxBookingsPerWeek;
        PriorityThreshold = priorityThreshold;
        AllowedStartTime = allowedStartTime;
        AllowedEndTime = allowedEndTime;
        AllowWeekends = allowWeekends;
    }

    public static BookingPolicy Default() => new();

    public static BookingPolicy Create(
        int maxBookingsPerDay,
        int maxBookingsPerWeek,
        int priorityThreshold,
        TimeSpan allowedStartTime,
        TimeSpan allowedEndTime,
        bool allowWeekends)
    {
        return new BookingPolicy(
            maxBookingsPerDay,
            maxBookingsPerWeek,
            priorityThreshold,
            allowedStartTime,
            allowedEndTime,
            allowWeekends);
    }

    public bool IsWithinTimeRestrictions(DateTime bookingStartUtc, DateTime bookingEndUtc)
    {
        var startTime = bookingStartUtc.TimeOfDay;
        var endTime = bookingEndUtc.TimeOfDay;

        return startTime >= AllowedStartTime && endTime <= AllowedEndTime;
    }

    public bool IsWeekendAllowed(DateTime bookingDate)
    {
        if (AllowWeekends)
        {
            return true;
        }

        return bookingDate.DayOfWeek != DayOfWeek.Saturday && bookingDate.DayOfWeek != DayOfWeek.Sunday;
    }

    public bool IsWithinDailyLimit(int currentDayBookings)
    {
        return currentDayBookings < MaxBookingsPerEmployeePerDay;
    }

    public bool IsWithinWeeklyLimit(int currentWeekBookings)
    {
        return currentWeekBookings < MaxBookingsPerEmployeePerWeek;
    }

    public bool MeetsPriorityRequirement(int employeePriority)
    {
        return employeePriority >= PriorityThreshold;
    }
}
