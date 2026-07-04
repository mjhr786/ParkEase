using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class ParkingPassRepository : Repository<ParkingPass>, IParkingPassRepository
{
    public ParkingPassRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<ParkingPass?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(p => p.User)
            .Include(p => p.ParkingSpace)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingPass>> GetActiveByUserIdAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ParkingSpace)
            .Where(p =>
                p.UserId == userId &&
                p.Duration.StartDateUtc <= utcNow &&
                p.Duration.EndDateUtc >= utcNow)
            .OrderBy(p => p.Duration.EndDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingPass>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ParkingSpace)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingPass>> GetCandidatePassesForBookingAsync(
        Guid userId,
        Guid parkingSpaceId,
        string? parkingZoneCode,
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedZoneCode = string.IsNullOrWhiteSpace(parkingZoneCode)
            ? null
            : parkingZoneCode.Trim().ToUpperInvariant();

        return await _dbSet
            .Include(p => p.User)
            .Include(p => p.ParkingSpace)
            .Where(p =>
                p.UserId == userId &&
                p.Duration.StartDateUtc <= bookingStartUtc &&
                p.Duration.EndDateUtc >= bookingEndUtc &&
                ((p.CoverageType == PassCoverageType.ParkingSpace && p.ParkingSpaceId == parkingSpaceId) ||
                 (p.CoverageType == PassCoverageType.ParkingZone && normalizedZoneCode != null && p.ParkingZoneCode == normalizedZoneCode)))
            .OrderBy(p => p.CoverageType == PassCoverageType.ParkingSpace ? 0 : 1)
            .ThenByDescending(p => p.DiscountPercentage)
            .ThenBy(p => p.Duration.EndDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> GetBookedHoursByDayAsync(
        Guid parkingPassId,
        Guid userId,
        DateTime bookingStartUtc,
        DateTime bookingEndUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.UserId == userId &&
                b.ParkingPassId == parkingPassId &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Rejected &&
                b.Status != BookingStatus.Expired &&
                b.StartDateTime < bookingEndUtc &&
                b.EndDateTime > bookingStartUtc);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        var bookings = await query.ToListAsync(cancellationToken);
        var hoursByDay = new Dictionary<DateOnly, decimal>();

        foreach (var booking in bookings)
        {
            foreach (var hoursForDay in SplitHoursByDay(booking.StartDateTime, booking.EndDateTime))
            {
                if (hoursByDay.TryGetValue(hoursForDay.Key, out var currentHours))
                {
                    hoursByDay[hoursForDay.Key] = currentHours + hoursForDay.Value;
                }
                else
                {
                    hoursByDay[hoursForDay.Key] = hoursForDay.Value;
                }
            }
        }

        return hoursByDay;
    }

    private static IReadOnlyDictionary<DateOnly, decimal> SplitHoursByDay(DateTime startUtc, DateTime endUtc)
    {
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
}
