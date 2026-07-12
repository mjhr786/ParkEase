using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Corporate;

public record GetMyCompaniesQuery(
    Guid UserId
) : IQuery<ApiResponse<List<CompanyDto>>>;

public record GetCompanyDashboardQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<CompanyDashboardDto>>;

public record GetMemberBookingsQuery(
    Guid CompanyId,
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    BookingStatus? Status = null,
    bool? IsVisitor = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null
) : IQuery<ApiResponse<MemberBookingsDto>>;

public record GetCompanyWaitlistQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<List<CorporateWaitlistDto>>>;

public record GetCompanyAllocationsQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<List<ParkingAllocationDto>>>;

public record GetCompanyParkingSpacesQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<List<CorporateParkingSpaceDto>>>;

public record GetVendorAllocationsQuery(
    Guid VendorId
) : IQuery<ApiResponse<List<VendorParkingAllocationDto>>>;

public record GetCompanyInvitationsQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<List<InvitationDto>>>;

public record GetCompanyMembersQuery(
    Guid CompanyId,
    Guid UserId,
    int Page = 1,
    int PageSize = 50
) : IQuery<ApiResponse<CompanyMembersDto>>;

public record GetCompanyDetailsQuery(
    Guid CompanyId,
    Guid UserId
) : IQuery<ApiResponse<CompanyDto>>;

public record GetCompanyInvoicesQuery(
    Guid CompanyId,
    Guid UserId,
    CorporateInvoiceStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<ApiResponse<CorporateInvoiceListDto>>;

public record GetCorporateInvoiceDetailsQuery(
    Guid CompanyId,
    Guid UserId,
    Guid InvoiceId
) : IQuery<ApiResponse<CorporateInvoiceDetailDto>>;

public class GetCompanyDetailsHandler : IQueryHandler<GetCompanyDetailsQuery, ApiResponse<CompanyDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyDetailsHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(GetCompanyDetailsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null)
        {
            return new ApiResponse<CompanyDto>(false, "Access denied. You are not a member of this company.", null);
        }

        var dto = await _readStore.GetCompanyDetailsAsync(query.CompanyId, ct);
        if (dto == null)
        {
            return new ApiResponse<CompanyDto>(false, "Company not found.", null);
        }

        return new ApiResponse<CompanyDto>(true, null, dto);
    }
}

public class GetCompanyMembersHandler : IQueryHandler<GetCompanyMembersQuery, ApiResponse<CompanyMembersDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyMembersHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<CompanyMembersDto>> HandleAsync(GetCompanyMembersQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CompanyMembersDto>(false, "Only company admins can view members.", null);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;

        var (members, totalCount) = await _readStore.GetCompanyMembersAsync(query.CompanyId, offset, pageSize, ct);

        return new ApiResponse<CompanyMembersDto>(
            true,
            null,
            new CompanyMembersDto(members.ToList(), totalCount, page, pageSize));
    }
}

public class GetCompanyInvitationsHandler : IQueryHandler<GetCompanyInvitationsQuery, ApiResponse<List<InvitationDto>>>
{
    private readonly ICorporateUnitOfWork _uow;

    public GetCompanyInvitationsHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<List<InvitationDto>>> HandleAsync(GetCompanyInvitationsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<List<InvitationDto>>(false, "Only company admins can view invitations.", null);
        }

        var invitations = await _uow.EmployeeInvitations.GetByCompanyIdAsync(query.CompanyId, ct);
        var now = DateTime.UtcNow;
        var dtos = invitations
            .Select(i =>
            {
                var status = i.Status;
                if (status == InvitationStatus.Pending && i.ExpiresAt <= now)
                {
                    status = InvitationStatus.Expired;
                }

                return new InvitationDto(
                    i.Id,
                    i.Email,
                    i.Role,
                    status,
                    i.ExpiresAt,
                    i.CreatedAt,
                    // Admins only reach this handler — expose token for copy-link UX.
                    status == InvitationStatus.Pending ? i.InvitationToken : null);
            })
            .ToList();

        return new ApiResponse<List<InvitationDto>>(true, null, dtos);
    }
}

public class GetCompanyAllocationsHandler : IQueryHandler<GetCompanyAllocationsQuery, ApiResponse<List<ParkingAllocationDto>>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyAllocationsHandler(ICorporateUnitOfWork uow, ICompanyQuotaCache quotaCache, ICompanyReadStore readStore)
    {
        _uow = uow;
        _quotaCache = quotaCache;
        _readStore = readStore;
    }

    public async Task<ApiResponse<List<ParkingAllocationDto>>> HandleAsync(GetCompanyAllocationsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<List<ParkingAllocationDto>>(false, "Access denied. You are not an active member of this company.", null);
        }

        var quotas = await _quotaCache.GetCompanyAllocationsAsync(query.CompanyId, ct);
        var assignmentsByAllocation = await _readStore.GetFixedAssignmentsByAllocationAsync(query.CompanyId, ct);

        var allocations = quotas.Select(q => new ParkingAllocationDto(
            q.AllocationId,
            q.CompanyId,
            q.ParkingSpaceId,
            q.ParkingSpaceTitle,
            q.TotalSlots,
            q.FixedSlots,
            q.SharedSlots,
            q.MonthlyRate,
            q.StartDate,
            q.EndDate,
            q.Status,
            q.SourceType,
            q.VendorId,
            q.LeaseReference,
            q.ApprovedByUserId,
            q.ApprovedAt,
            new BookingPolicyDto(
                q.MaxBookingsPerEmployeePerDay,
                q.MaxBookingsPerEmployeePerWeek,
                q.PriorityThreshold,
                q.AllowedStartTime,
                q.AllowedEndTime,
                q.AllowWeekends),
            assignmentsByAllocation.GetValueOrDefault(q.AllocationId) ?? new List<FixedSlotAssignmentDto>(),
            q.CreatedAt,
            q.VendorName)).ToList();

        return new ApiResponse<List<ParkingAllocationDto>>(true, null, allocations);
    }
}

public class GetCompanyParkingSpacesHandler : IQueryHandler<GetCompanyParkingSpacesQuery, ApiResponse<List<CorporateParkingSpaceDto>>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyParkingSpacesHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<List<CorporateParkingSpaceDto>>> HandleAsync(GetCompanyParkingSpacesQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<List<CorporateParkingSpaceDto>>(false, "Only company admins can view company-owned parking.", null);
        }

        var spaces = await _readStore.GetCompanyOwnedParkingSpacesAsync(query.CompanyId, ct);
        return new ApiResponse<List<CorporateParkingSpaceDto>>(true, null, spaces.ToList());
    }
}

public class GetMemberBookingsHandler : IQueryHandler<GetMemberBookingsQuery, ApiResponse<MemberBookingsDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetMemberBookingsHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<MemberBookingsDto>> HandleAsync(GetMemberBookingsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<MemberBookingsDto>(false, "Access denied.", null);
        }

        var page = Math.Max(1, query.Page);
        // Export uses larger page sizes (up to 5000); UI list caps at 200.
        var pageSize = Math.Clamp(query.PageSize, 1, 5000);
        var offset = (page - 1) * pageSize;

        // Admins see company-wide bookings; employees see only their own (same pattern as waitlist).
        var onlyOwnBookings = !membership.IsAdmin;
        var filter = new CorporateBookingListFilter(query.Status, query.IsVisitor, query.FromUtc, query.ToUtc);
        var (bookings, totalCount) = await _readStore.GetMemberBookingsAsync(
            query.CompanyId, membership.Id, onlyOwnBookings, offset, pageSize, filter, ct);

        return new ApiResponse<MemberBookingsDto>(
            true,
            null,
            new MemberBookingsDto(bookings.ToList(), totalCount, page, pageSize));
    }
}

public class GetVendorAllocationsHandler : IQueryHandler<GetVendorAllocationsQuery, ApiResponse<List<VendorParkingAllocationDto>>>
{
    private readonly ICompanyReadStore _readStore;

    public GetVendorAllocationsHandler(ICompanyReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<ApiResponse<List<VendorParkingAllocationDto>>> HandleAsync(GetVendorAllocationsQuery query, CancellationToken ct = default)
    {
        var allocations = await _readStore.GetVendorAllocationsAsync(query.VendorId, ct);
        return new ApiResponse<List<VendorParkingAllocationDto>>(true, null, allocations.ToList());
    }
}

public class GetCompanyWaitlistHandler : IQueryHandler<GetCompanyWaitlistQuery, ApiResponse<List<CorporateWaitlistDto>>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyWaitlistHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<List<CorporateWaitlistDto>>> HandleAsync(GetCompanyWaitlistQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<List<CorporateWaitlistDto>>(false, "Access denied.", null);
        }

        var onlyOwnEntries = !membership.IsAdmin;
        var waitlist = await _readStore.GetCompanyWaitlistAsync(
            query.CompanyId, membership.Id, onlyOwnEntries, ct);

        return new ApiResponse<List<CorporateWaitlistDto>>(true, null, waitlist.ToList());
    }
}

public class GetCompanyDashboardHandler : IQueryHandler<GetCompanyDashboardQuery, ApiResponse<CompanyDashboardDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;
    private readonly ICacheService _cache;

    public GetCompanyDashboardHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore, ICacheService cache)
    {
        _uow = uow;
        _readStore = readStore;
        _cache = cache;
    }

    public async Task<ApiResponse<CompanyDashboardDto>> HandleAsync(GetCompanyDashboardQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CompanyDashboardDto>(false, "Only company admins can view the dashboard.", null);
        }

        var cacheKey = CacheKeys.CompanyDashboard(query.CompanyId);
        var cached = await _cache.GetAsync<CompanyDashboardDto>(cacheKey, ct);
        if (cached != null)
            return new ApiResponse<CompanyDashboardDto>(true, null, cached);

        var dashboard = await _readStore.GetCompanyDashboardAsync(query.CompanyId, DateTime.UtcNow, ct);
        await _cache.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(2), ct);
        return new ApiResponse<CompanyDashboardDto>(true, null, dashboard);
    }
}

public class GetMyCompaniesHandler : IQueryHandler<GetMyCompaniesQuery, ApiResponse<List<CompanyDto>>>
{
    private readonly ICompanyReadStore _readStore;

    public GetMyCompaniesHandler(ICompanyReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<ApiResponse<List<CompanyDto>>> HandleAsync(GetMyCompaniesQuery query, CancellationToken ct = default)
    {
        var companies = await _readStore.GetMyCompaniesAsync(query.UserId, ct);
        return new ApiResponse<List<CompanyDto>>(true, null, companies.ToList());
    }
}

public class GetCompanyInvoicesHandler : IQueryHandler<GetCompanyInvoicesQuery, ApiResponse<CorporateInvoiceListDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCompanyInvoicesHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<CorporateInvoiceListDto>> HandleAsync(GetCompanyInvoicesQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceListDto>(false, "Only company admins can view invoices.", null);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
        var offset = (page - 1) * pageSize;

        var (items, total) = await _readStore.GetCompanyInvoicesAsync(
            query.CompanyId,
            query.Status,
            offset,
            pageSize,
            ct);

        return new ApiResponse<CorporateInvoiceListDto>(
            true,
            null,
            new CorporateInvoiceListDto(items.ToList(), total, page, pageSize));
    }
}

public class GetCorporateInvoiceDetailsHandler
    : IQueryHandler<GetCorporateInvoiceDetailsQuery, ApiResponse<CorporateInvoiceDetailDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanyReadStore _readStore;

    public GetCorporateInvoiceDetailsHandler(ICorporateUnitOfWork uow, ICompanyReadStore readStore)
    {
        _uow = uow;
        _readStore = readStore;
    }

    public async Task<ApiResponse<CorporateInvoiceDetailDto>> HandleAsync(
        GetCorporateInvoiceDetailsQuery query,
        CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Only company admins can view invoices.", null);
        }

        var detail = await _readStore.GetCorporateInvoiceDetailAsync(query.CompanyId, query.InvoiceId, ct);
        if (detail == null)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Invoice not found.", null);
        }

        return new ApiResponse<CorporateInvoiceDetailDto>(true, null, detail);
    }
}
