using Dapper;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

// ══════════════════════════════════════════════════════
// CompanyRepository
// ══════════════════════════════════════════════════════

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Company?> GetWithMembershipsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Memberships.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.User)
            .Include(c => c.Invitations.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetWithAllocationsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Allocations.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.ParkingSpace)
            .Include(c => c.Allocations.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.FixedAssignments.Where(f => !f.IsDeleted))
            .Include(c => c.Memberships.Where(m => !m.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetFullAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Memberships.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.User)
            .Include(c => c.Allocations.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.ParkingSpace)
            .Include(c => c.Allocations.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.FixedAssignments.Where(f => !f.IsDeleted))
                    .ThenInclude(f => f.Membership)
                        .ThenInclude(m => m.User)
            .Include(c => c.Invitations.Where(i => !i.IsDeleted))
            .Include(c => c.WaitlistEntries.Where(w => !w.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetAggregateForBookingAsync(
        Guid companyId,
        Guid userId,
        Guid allocationId,
        DateTime bookingStart,
        DateTime bookingEnd,
        CancellationToken cancellationToken = default)
    {
        var usageDate = DateOnly.FromDateTime(bookingStart);

        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Memberships.Where(m => !m.IsDeleted && m.UserId == userId))
            .Include(c => c.Allocations.Where(a => !a.IsDeleted && a.Id == allocationId))
                .ThenInclude(a => a.FixedAssignments.Where(f => !f.IsDeleted))
            .Include(c => c.Usages.Where(u => !u.IsDeleted && u.AllocationId == allocationId && u.UsageDate == usageDate))
            .Include(c => c.WaitlistEntries.Where(w =>
                !w.IsDeleted &&
                w.AllocationId == allocationId &&
                w.Status == WaitlistStatus.Pending &&
                w.RequestedStartDateTime < bookingEnd &&
                w.RequestedEndDateTime > bookingStart))
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<Company?> GetAggregateForInvitationAcceptanceAsync(
        string invitationToken,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Invitations.Where(i => !i.IsDeleted && i.InvitationToken == invitationToken))
            .Include(c => c.Memberships.Where(m => !m.IsDeleted && m.UserId == userId))
            .FirstOrDefaultAsync(
                c => c.Invitations.Any(i => !i.IsDeleted && i.InvitationToken == invitationToken),
                cancellationToken);
    }

    public async Task<Company?> GetAggregateByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Allocations.Where(a => !a.IsDeleted && a.Id == allocationId))
            .FirstOrDefaultAsync(
                c => c.Allocations.Any(a => !a.IsDeleted && a.Id == allocationId),
                cancellationToken);
    }

    public async Task<Company?> GetAggregateForWaitlistPromotionAsync(
        Guid companyId,
        Guid waitlistEntryId,
        Guid? adminUserId,
        CancellationToken cancellationToken = default)
    {
        // Resolve the entry first so includes can target one allocation/membership only.
        var entryMeta = await _context.Set<CorporateWaitlistEntry>()
            .AsNoTracking()
            .Where(w => w.Id == waitlistEntryId && w.CompanyId == companyId && !w.IsDeleted)
            .Select(w => new
            {
                w.MembershipId,
                w.AllocationId,
                w.RequestedStartDateTime,
                w.RequestedEndDateTime
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (entryMeta == null)
        {
            return null;
        }

        var usageDate = DateOnly.FromDateTime(entryMeta.RequestedStartDateTime);
        var allocationId = entryMeta.AllocationId;
        var membershipId = entryMeta.MembershipId;
        var bookingStart = entryMeta.RequestedStartDateTime;
        var bookingEnd = entryMeta.RequestedEndDateTime;

        return await _dbSet
            .AsSplitQuery()
            .Include(c => c.Memberships.Where(m =>
                !m.IsDeleted &&
                (m.Id == membershipId || (adminUserId.HasValue && m.UserId == adminUserId.Value))))
            .Include(c => c.Allocations.Where(a => !a.IsDeleted && a.Id == allocationId))
                .ThenInclude(a => a.FixedAssignments.Where(f => !f.IsDeleted))
            .Include(c => c.Usages.Where(u =>
                !u.IsDeleted && u.AllocationId == allocationId && u.UsageDate == usageDate))
            .Include(c => c.WaitlistEntries.Where(w =>
                !w.IsDeleted &&
                w.AllocationId == allocationId &&
                (w.Id == waitlistEntryId ||
                 (w.Status == WaitlistStatus.Pending &&
                  w.RequestedStartDateTime < bookingEnd &&
                  w.RequestedEndDateTime > bookingStart))))
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
    }

    public async Task<bool> IsUserMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserCompanyMembership>()
            .AnyAsync(m => m.CompanyId == companyId && m.UserId == userId && m.IsActive && !m.IsDeleted, cancellationToken);
    }

    public async Task<UserCompanyMembership?> GetMembershipAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserCompanyMembership>()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == userId && !m.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByRegistrationNumberAsync(string registrationNumber, CancellationToken cancellationToken = default)
    {
        var normalized = registrationNumber.Trim().ToUpperInvariant();
        return await _dbSet.AnyAsync(c => c.RegistrationNumber == normalized && !c.IsDeleted, cancellationToken);
    }
}

// ══════════════════════════════════════════════════════
// CorporateBookingRepository
// ══════════════════════════════════════════════════════

public class CorporateBookingRepository : Repository<CorporateBooking>, ICorporateBookingRepository
{
    public CorporateBookingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<CorporateBooking?> GetByCompanyAndBookingIdAsync(
        Guid companyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(cb => cb.Membership)
            .FirstOrDefaultAsync(
                cb => cb.CompanyId == companyId
                    && cb.BookingId == bookingId
                    && !cb.IsDeleted,
                cancellationToken);
    }

    public async Task<IEnumerable<CorporateBooking>> GetByCompanyIdAsync(Guid companyId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(cb => cb.Booking)
            .Include(cb => cb.Membership)
                .ThenInclude(m => m.User)
            .Where(cb => cb.CompanyId == companyId)
            .OrderByDescending(cb => cb.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CorporateBooking>> GetByMembershipIdAsync(Guid companyId, Guid membershipId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(cb => cb.Booking)
            .Where(cb => cb.CompanyId == companyId && cb.MembershipId == membershipId)
            .OrderByDescending(cb => cb.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetMembershipBookingCountForDateAsync(Guid companyId, Guid membershipId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await _dbSet
            .CountAsync(cb => cb.CompanyId == companyId
                && cb.MembershipId == membershipId
                && cb.Booking.StartDateTime >= dayStart
                && cb.Booking.StartDateTime < dayEnd
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected,
                cancellationToken);
    }

    public async Task<int> GetMembershipBookingCountForWeekAsync(Guid companyId, Guid membershipId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        var start = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await _dbSet
            .CountAsync(cb => cb.CompanyId == companyId
                && cb.MembershipId == membershipId
                && cb.Booking.StartDateTime >= start
                && cb.Booking.StartDateTime < end
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected,
                cancellationToken);
    }

    public async Task<int> GetActiveSharedBookingsCountAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(cb => cb.CompanyId == companyId
                && cb.AllocationId == allocationId
                && cb.SlotType == CorporateSlotType.Shared
                && cb.Booking.StartDateTime < end
                && cb.Booking.EndDateTime > start
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected
                && cb.Booking.Status != BookingStatus.Expired,
                cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetOccupiedSharedSlotNumbersAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(cb => cb.CompanyId == companyId
                && cb.AllocationId == allocationId
                && cb.SlotType == CorporateSlotType.Shared
                && cb.Booking.StartDateTime < end
                && cb.Booking.EndDateTime > start
                && cb.Booking.SlotNumber.HasValue
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected
                && cb.Booking.Status != BookingStatus.Expired)
            .Select(cb => cb.Booking.SlotNumber!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetSharedSlotUsageCountsAsync(Guid companyId, Guid allocationId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var rows = await _dbSet
            .Where(cb => cb.CompanyId == companyId
                && cb.AllocationId == allocationId
                && cb.SlotType == CorporateSlotType.Shared
                && cb.Booking.SlotNumber.HasValue
                && cb.Booking.StartDateTime >= sinceUtc
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected)
            .GroupBy(cb => cb.Booking.SlotNumber!.Value)
            .Select(g => new SharedSlotUsageRow
            {
                SlotNumber = g.Key,
                UsageCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.SlotNumber, r => r.UsageCount);
    }

    public async Task<bool> HasOverlappingBookingAsync(Guid companyId, Guid membershipId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(cb => cb.CompanyId == companyId
            && cb.MembershipId == membershipId
            && cb.Booking.StartDateTime < end
            && cb.Booking.EndDateTime > start
            && cb.Booking.Status != BookingStatus.Cancelled
            && cb.Booking.Status != BookingStatus.Rejected
            && cb.Booking.Status != BookingStatus.Expired,
            cancellationToken);
    }

    public async Task<bool> HasOverlappingVehicleBookingAsync(Guid companyId, Guid allocationId, string vehicleNumber, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vehicleNumber))
        {
            return false;
        }

        var normalizedVehicleNumber = vehicleNumber.Trim().ToUpperInvariant();

        return await _dbSet.AnyAsync(cb => cb.CompanyId == companyId
            && cb.AllocationId == allocationId
            && cb.Booking.StartDateTime < end
            && cb.Booking.EndDateTime > start
            && cb.Booking.VehicleNumber != null
            && cb.Booking.VehicleNumber.ToUpper() == normalizedVehicleNumber
            && cb.Booking.Status != BookingStatus.Cancelled
            && cb.Booking.Status != BookingStatus.Rejected
            && cb.Booking.Status != BookingStatus.Expired,
            cancellationToken);
    }

    public async Task<int> GetRecentBookingCreateCountAsync(Guid companyId, Guid membershipId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(cb => cb.CompanyId == companyId
            && cb.MembershipId == membershipId
            && cb.CreatedAt >= sinceUtc,
            cancellationToken);
    }

    public async Task<int> GetCompanyBookingCountAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(cb => cb.CompanyId == companyId, cancellationToken);
    }

    public async Task<CorporateReservationPreCheck> GetReservationPreCheckAsync(
        Guid companyId,
        Guid membershipId,
        Guid allocationId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        DateOnly usageDate,
        DateOnly weekStart,
        DateTime recentCreatesSinceUtc,
        DateTime sharedUsageSinceUtc,
        string? vehicleNumber,
        CancellationToken cancellationToken = default)
    {
        var dayStart = usageDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = usageDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var weekEnd = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var weekStartUtc = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var normalizedVehicle = string.IsNullOrWhiteSpace(vehicleNumber)
            ? null
            : vehicleNumber.Trim().ToUpperInvariant();

        // Terminal marketplace statuses excluded from capacity / overlap (Cancelled=4, Expired=5, Rejected=7)
        const string sql = """
            SELECT
                CAST((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                      AND cb."MembershipId" = @MembershipId
                      AND cb."IsDeleted" = FALSE
                      AND b."IsDeleted" = FALSE
                      AND b."StartDateTime" >= @DayStart
                      AND b."StartDateTime" < @DayEnd
                      AND b."Status" NOT IN (4, 7)
                ) AS INTEGER) AS DayBookingCount,
                CAST((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                      AND cb."MembershipId" = @MembershipId
                      AND cb."IsDeleted" = FALSE
                      AND b."IsDeleted" = FALSE
                      AND b."StartDateTime" >= @WeekStart
                      AND b."StartDateTime" < @WeekEnd
                      AND b."Status" NOT IN (4, 7)
                ) AS INTEGER) AS WeekBookingCount,
                CAST((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                      AND cb."AllocationId" = @AllocationId
                      AND cb."SlotType" = 1
                      AND cb."IsDeleted" = FALSE
                      AND b."IsDeleted" = FALSE
                      AND b."StartDateTime" < @WindowEnd
                      AND b."EndDateTime" > @WindowStart
                      AND b."Status" NOT IN (4, 5, 7)
                ) AS INTEGER) AS ActiveSharedBookingCount,
                CAST((
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM "CorporateBookings" cb
                        INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                        WHERE cb."CompanyId" = @CompanyId
                          AND cb."MembershipId" = @MembershipId
                          AND cb."IsDeleted" = FALSE
                          AND b."IsDeleted" = FALSE
                          AND b."StartDateTime" < @WindowEnd
                          AND b."EndDateTime" > @WindowStart
                          AND b."Status" NOT IN (4, 5, 7)
                    ) THEN 1 ELSE 0 END
                ) AS INTEGER) AS HasOverlappingMemberBooking,
                CAST((
                    SELECT CASE
                        WHEN @VehicleNumber IS NULL THEN 0
                        WHEN EXISTS (
                            SELECT 1
                            FROM "CorporateBookings" cb
                            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                            WHERE cb."CompanyId" = @CompanyId
                              AND cb."AllocationId" = @AllocationId
                              AND cb."IsDeleted" = FALSE
                              AND b."IsDeleted" = FALSE
                              AND b."VehicleNumber" IS NOT NULL
                              AND UPPER(b."VehicleNumber") = @VehicleNumber
                              AND b."StartDateTime" < @WindowEnd
                              AND b."EndDateTime" > @WindowStart
                              AND b."Status" NOT IN (4, 5, 7)
                        ) THEN 1 ELSE 0 END
                ) AS INTEGER) AS HasOverlappingVehicleBooking,
                CAST((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    WHERE cb."CompanyId" = @CompanyId
                      AND cb."MembershipId" = @MembershipId
                      AND cb."IsDeleted" = FALSE
                      AND cb."CreatedAt" >= @RecentCreatesSince
                ) AS INTEGER) AS RecentBookingCreateCount;

            SELECT DISTINCT b."SlotNumber" AS SlotNumber
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
              AND cb."AllocationId" = @AllocationId
              AND cb."SlotType" = 1
              AND cb."IsDeleted" = FALSE
              AND b."IsDeleted" = FALSE
              AND b."SlotNumber" IS NOT NULL
              AND b."StartDateTime" < @WindowEnd
              AND b."EndDateTime" > @WindowStart
              AND b."Status" NOT IN (4, 5, 7);

            SELECT b."SlotNumber" AS SlotNumber, CAST(COUNT(*) AS INTEGER) AS UsageCount
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
              AND cb."AllocationId" = @AllocationId
              AND cb."SlotType" = 1
              AND cb."IsDeleted" = FALSE
              AND b."IsDeleted" = FALSE
              AND b."SlotNumber" IS NOT NULL
              AND b."StartDateTime" >= @SharedUsageSince
              AND b."Status" NOT IN (4, 7)
            GROUP BY b."SlotNumber";
            """;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                CompanyId = companyId,
                MembershipId = membershipId,
                AllocationId = allocationId,
                WindowStart = windowStartUtc,
                WindowEnd = windowEndUtc,
                DayStart = dayStart,
                DayEnd = dayEnd,
                WeekStart = weekStartUtc,
                WeekEnd = weekEnd,
                RecentCreatesSince = recentCreatesSinceUtc,
                SharedUsageSince = sharedUsageSinceUtc,
                VehicleNumber = normalizedVehicle
            },
            cancellationToken: cancellationToken));

        var scalars = await multi.ReadSingleAsync<PreCheckScalarRow>();
        var occupied = (await multi.ReadAsync<int?>())
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
        var usageRows = (await multi.ReadAsync<SharedSlotUsageRow>()).ToList();
        var usageMap = usageRows.ToDictionary(r => r.SlotNumber, r => r.UsageCount);

        return new CorporateReservationPreCheck
        {
            DayBookingCount = scalars.DayBookingCount,
            WeekBookingCount = scalars.WeekBookingCount,
            ActiveSharedBookingCount = scalars.ActiveSharedBookingCount,
            OccupiedSharedSlotNumbers = occupied,
            SharedSlotUsageBySlot = usageMap,
            HasOverlappingMemberBooking = scalars.HasOverlappingMemberBooking != 0,
            HasOverlappingVehicleBooking = scalars.HasOverlappingVehicleBooking != 0,
            RecentBookingCreateCount = scalars.RecentBookingCreateCount
        };
    }

    private sealed class PreCheckScalarRow
    {
        public int DayBookingCount { get; set; }
        public int WeekBookingCount { get; set; }
        public int ActiveSharedBookingCount { get; set; }
        public int HasOverlappingMemberBooking { get; set; }
        public int HasOverlappingVehicleBooking { get; set; }
        public int RecentBookingCreateCount { get; set; }
    }

    public async Task<IReadOnlyList<CorporateBooking>> GetBillableBookingsForPeriodAsync(
        Guid companyId,
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(cb => cb.Booking)
                .ThenInclude(b => b.ParkingSpace)
            .Include(cb => cb.Membership)
                .ThenInclude(m => m.User)
            .Where(cb => cb.CompanyId == companyId
                && !cb.IsDeleted
                && cb.Booking.StartDateTime >= periodStartUtc
                && cb.Booking.StartDateTime < periodEndExclusiveUtc
                && cb.Booking.Status != BookingStatus.Cancelled
                && cb.Booking.Status != BookingStatus.Rejected
                && cb.Booking.TotalAmount > 0)
            .OrderBy(cb => cb.Booking.StartDateTime)
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class SharedSlotUsageRow
{
    public int SlotNumber { get; set; }
    public int UsageCount { get; set; }
}

public class EmployeeInvitationRepository : Repository<EmployeeInvitation>, IEmployeeInvitationRepository
{
    public EmployeeInvitationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<bool> HasPendingInvitationAsync(Guid companyId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _dbSet.AnyAsync(i =>
            i.CompanyId == companyId &&
            !i.IsDeleted &&
            i.Status == InvitationStatus.Pending &&
            i.Email == normalizedEmail &&
            i.ExpiresAt > DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInvitation>> GetByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && !i.IsDeleted)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════
// CorporateInvoiceRepository
// ══════════════════════════════════════════════════════

public class CorporateInvoiceRepository : Repository<CorporateInvoice>, ICorporateInvoiceRepository
{
    public CorporateInvoiceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<CorporateInvoice?> GetByIdWithLinesAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.LineItems.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(
                i => i.Id == invoiceId && i.CompanyId == companyId && !i.IsDeleted,
                cancellationToken);
    }

    public async Task<bool> ExistsNonVoidForPeriodAsync(
        Guid companyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(
            i => i.CompanyId == companyId
                && !i.IsDeleted
                && i.Status != CorporateInvoiceStatus.Void
                && i.PeriodStart == periodStart
                && i.PeriodEnd == periodEnd,
            cancellationToken);
    }
}
