using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ParkingApp.Corporate.Domain;
namespace ParkingApp.Corporate.Infrastructure;

/// <summary>
/// Corporate module persistence facade. Repositories depend on this instead of the full ApplicationDbContext.
/// </summary>
public interface ICorporateDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<UserCompanyMembership> UserCompanyMemberships { get; }
    DbSet<ParkingAllocation> ParkingAllocations { get; }
    DbSet<CorporateBooking> CorporateBookings { get; }
    DbSet<FixedSlotAssignment> FixedSlotAssignments { get; }
    DbSet<EmployeeInvitation> EmployeeInvitations { get; }
    DbSet<CompanyUsage> CompanyUsages { get; }
    DbSet<CorporateWaitlistEntry> CorporateWaitlistEntries { get; }
    DbSet<CorporateInvoice> CorporateInvoices { get; }
    DbSet<CorporateInvoiceLineItem> CorporateInvoiceLineItems { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

