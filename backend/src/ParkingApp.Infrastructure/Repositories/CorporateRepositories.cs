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
