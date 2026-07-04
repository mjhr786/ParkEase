namespace ParkingApp.Domain.ValueObjects;

public sealed record Duration
{
    public DateTime StartDateUtc { get; private init; }
    public DateTime EndDateUtc { get; private init; }

    private Duration()
    {
    }

    private Duration(DateTime startDateUtc, DateTime endDateUtc)
    {
        var normalizedStart = Normalize(startDateUtc);
        var normalizedEnd = Normalize(endDateUtc);

        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("Pass end date must be after the start date.");
        }

        StartDateUtc = normalizedStart;
        EndDateUtc = normalizedEnd;
    }

    public static Duration Create(DateTime startDateUtc, DateTime endDateUtc) => new(startDateUtc, endDateUtc);

    public bool IsActiveOn(DateTime utcNow)
    {
        var now = Normalize(utcNow);
        return now >= StartDateUtc && now <= EndDateUtc;
    }

    public bool IsExpiredOn(DateTime utcNow) => Normalize(utcNow) > EndDateUtc;

    public bool Covers(DateTime bookingStartUtc, DateTime bookingEndUtc)
    {
        var normalizedStart = Normalize(bookingStartUtc);
        var normalizedEnd = Normalize(bookingEndUtc);
        return normalizedStart >= StartDateUtc && normalizedEnd <= EndDateUtc;
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
