using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Domain.Interfaces;

// Corporate module unit-of-work and repository ports.

public interface ICorporateUnitOfWork : IUnitOfWorkTransaction
{
    ICompanyRepository Companies { get; }
    ICorporateBookingRepository CorporateBookings { get; }
    IEmployeeInvitationRepository EmployeeInvitations { get; }
    ICorporateInvoiceRepository Invoices { get; }
}

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetWithMembershipsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Company?> GetWithAllocationsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Company?> GetFullAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<Company?> GetAggregateForBookingAsync(Guid companyId, Guid userId, Guid allocationId, DateTime bookingStart, DateTime bookingEnd, CancellationToken cancellationToken = default);
    Task<Company?> GetAggregateForInvitationAcceptanceAsync(string invitationToken, Guid userId, CancellationToken cancellationToken = default);
    Task<Company?> GetAggregateByAllocationAsync(Guid allocationId, CancellationToken cancellationToken = default);
    Task<Company?> GetAggregateForWaitlistPromotionAsync(Guid companyId, Guid waitlistEntryId, Guid? adminUserId, CancellationToken cancellationToken = default);
    Task<bool> IsUserMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
    Task<UserCompanyMembership?> GetMembershipAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByRegistrationNumberAsync(string registrationNumber, CancellationToken cancellationToken = default);
}

public interface ICorporateBookingRepository : IRepository<CorporateBooking>
{
    Task<CorporateBooking?> GetByCompanyAndBookingIdAsync(Guid companyId, Guid bookingId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CorporateBooking>> GetByCompanyIdAsync(Guid companyId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<CorporateBooking>> GetByMembershipIdAsync(Guid companyId, Guid membershipId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetMembershipBookingCountForDateAsync(Guid companyId, Guid membershipId, DateOnly date, CancellationToken cancellationToken = default);
    Task<int> GetMembershipBookingCountForWeekAsync(Guid companyId, Guid membershipId, DateOnly weekStart, CancellationToken cancellationToken = default);
    Task<int> GetActiveSharedBookingsCountAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetOccupiedSharedSlotNumbersAsync(Guid companyId, Guid allocationId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetSharedSlotUsageCountsAsync(Guid companyId, Guid allocationId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(Guid companyId, Guid membershipId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingVehicleBookingAsync(Guid companyId, Guid allocationId, string vehicleNumber, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetRecentBookingCreateCountAsync(Guid companyId, Guid membershipId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<int> GetCompanyBookingCountAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CorporateReservationPreCheck> GetReservationPreCheckAsync(
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
        VehicleClass vehicleClass,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorporateBooking>> GetBillableBookingsForPeriodAsync(
        Guid companyId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int maxRows,
        CancellationToken cancellationToken = default);
}

public interface IEmployeeInvitationRepository : IRepository<EmployeeInvitation>
{
    Task<bool> HasPendingInvitationAsync(Guid companyId, string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeInvitation>> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public interface ICorporateInvoiceRepository : IRepository<CorporateInvoice>
{
    Task<CorporateInvoice?> GetByIdWithLinesAsync(Guid companyId, Guid invoiceId, CancellationToken cancellationToken = default);
    Task<bool> ExistsNonVoidForPeriodAsync(Guid companyId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);
}
