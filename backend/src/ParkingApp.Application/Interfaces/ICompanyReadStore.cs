using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Read-model port for corporate (company) query side. Implementations live in Infrastructure (Dapper/SQL).
/// </summary>
public interface ICompanyReadStore
{
    Task<IReadOnlyList<CompanyDto>> GetMyCompaniesAsync(Guid userId, CancellationToken ct = default);

    Task<CompanyDto?> GetCompanyDetailsAsync(Guid companyId, CancellationToken ct = default);

    Task<(IReadOnlyList<MembershipDto> Members, int TotalCount)> GetCompanyMembersAsync(
        Guid companyId,
        int offset,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Fixed-slot assignments keyed by allocation id.</summary>
    Task<IReadOnlyDictionary<Guid, List<FixedSlotAssignmentDto>>> GetFixedAssignmentsByAllocationAsync(
        Guid companyId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CorporateParkingSpaceDto>> GetCompanyOwnedParkingSpacesAsync(
        Guid companyId,
        CancellationToken ct = default);

    /// <param name="onlyOwnBookings">When true, filter to <paramref name="membershipId"/>; when false (company admin), return all company bookings.</param>
    Task<(IReadOnlyList<CorporateBookingDto> Bookings, int TotalCount)> GetMemberBookingsAsync(
        Guid companyId,
        Guid membershipId,
        bool onlyOwnBookings,
        int offset,
        int pageSize,
        CorporateBookingListFilter? filter = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<VendorParkingAllocationDto>> GetVendorAllocationsAsync(
        Guid vendorId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CorporateWaitlistDto>> GetCompanyWaitlistAsync(
        Guid companyId,
        Guid membershipId,
        bool onlyOwnEntries,
        CancellationToken ct = default);

    Task<CompanyDashboardDto> GetCompanyDashboardAsync(
        Guid companyId,
        DateTime utcNow,
        CancellationToken ct = default);

    Task<(IReadOnlyList<CorporateInvoiceSummaryDto> Items, int TotalCount)> GetCompanyInvoicesAsync(
        Guid companyId,
        CorporateInvoiceStatus? status,
        int offset,
        int pageSize,
        CancellationToken ct = default);

    Task<CorporateInvoiceDetailDto?> GetCorporateInvoiceDetailAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken ct = default);
}
