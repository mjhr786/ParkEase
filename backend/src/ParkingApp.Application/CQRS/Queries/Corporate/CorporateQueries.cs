using Dapper;
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
    int PageSize = 20
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

public class GetCompanyDetailsHandler : IQueryHandler<GetCompanyDetailsQuery, ApiResponse<CompanyDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyDetailsHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(GetCompanyDetailsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null)
        {
            return new ApiResponse<CompanyDto>(false, "Access denied. You are not a member of this company.", null);
        }

        const string sql = """
            SELECT
                c."Id" AS Id,
                c."Name" AS Name,
                c."RegistrationNumber" AS RegistrationNumber,
                c."ContactEmail" AS ContactEmail,
                c."ContactPhone" AS ContactPhone,
                c."BillingAddress" AS BillingAddress,
                c."BillingType" AS BillingType,
                c."IsActive" AS IsActive,
                c."CreatedAt" AS CreatedAt,
                COALESCE(m."MemberCount", 0) AS MemberCount,
                COALESCE(a."ActiveAllocationCount", 0) AS ActiveAllocationCount
            FROM "Companies" c
            LEFT JOIN (
                SELECT "CompanyId", COUNT(*) AS "MemberCount"
                FROM "UserCompanyMemberships"
                WHERE "CompanyId" = @CompanyId AND "IsDeleted" = FALSE
                GROUP BY "CompanyId"
            ) m ON m."CompanyId" = c."Id"
            LEFT JOIN (
                SELECT "CompanyId", COUNT(*) AS "ActiveAllocationCount"
                FROM "ParkingAllocations"
                WHERE "CompanyId" = @CompanyId
                    AND "IsDeleted" = FALSE
                    AND "Status" = @ActiveStatus
                GROUP BY "CompanyId"
            ) a ON a."CompanyId" = c."Id"
            WHERE c."Id" = @CompanyId AND c."IsDeleted" = FALSE;
            """;

        using var connection = _sql.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<CompanyDetailsRow>(
            new CommandDefinition(
                sql,
                new { query.CompanyId, ActiveStatus = (int)AllocationStatus.Active },
                cancellationToken: ct));

        if (row == null)
        {
            return new ApiResponse<CompanyDto>(false, "Company not found.", null);
        }

        var dto = new CompanyDto(
            row.Id,
            row.Name,
            row.RegistrationNumber,
            row.ContactEmail,
            row.ContactPhone,
            row.BillingAddress,
            (BillingType)row.BillingType,
            row.IsActive,
            row.MemberCount,
            row.ActiveAllocationCount,
            row.CreatedAt);

        return new ApiResponse<CompanyDto>(true, null, dto);
    }
}

public class GetCompanyMembersHandler : IQueryHandler<GetCompanyMembersQuery, ApiResponse<CompanyMembersDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyMembersHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
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

        const string sql = """
            SELECT
                m."Id" AS Id,
                m."UserId" AS UserId,
                TRIM(COALESCE(u."FirstName", '') || ' ' || COALESCE(u."LastName", '')) AS UserName,
                u."Email" AS UserEmail,
                m."Role" AS Role,
                m."EmployeeCode" AS EmployeeCode,
                m."Priority" AS Priority,
                m."IsActive" AS IsActive,
                m."CreatedAt" AS CreatedAt
            FROM "UserCompanyMemberships" m
            INNER JOIN "Users" u ON u."Id" = m."UserId"
            WHERE m."CompanyId" = @CompanyId
                AND m."IsDeleted" = FALSE
                AND u."IsDeleted" = FALSE
            ORDER BY m."Role" DESC, m."CreatedAt" DESC
            OFFSET @Offset
            LIMIT @PageSize;

            SELECT COUNT(*)
            FROM "UserCompanyMemberships" m
            INNER JOIN "Users" u ON u."Id" = m."UserId"
            WHERE m."CompanyId" = @CompanyId
                AND m."IsDeleted" = FALSE
                AND u."IsDeleted" = FALSE;
            """;

        using var connection = _sql.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                sql,
                new { query.CompanyId, Offset = offset, PageSize = pageSize },
                cancellationToken: ct));

        var rows = (await multi.ReadAsync<MembershipRow>()).ToList();

        var members = rows.Select(r => new MembershipDto(
            r.Id,
            r.UserId,
            r.UserName,
            r.UserEmail,
            (CompanyRole)r.Role,
            r.EmployeeCode,
            r.Priority,
            r.IsActive,
            r.CreatedAt)).ToList();

        var totalCount = await multi.ReadSingleAsync<int>();

        return new ApiResponse<CompanyMembersDto>(
            true,
            null,
            new CompanyMembersDto(members, totalCount, page, pageSize));
    }
}

public class GetCompanyAllocationsHandler : IQueryHandler<GetCompanyAllocationsQuery, ApiResponse<List<ParkingAllocationDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyAllocationsHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _quotaCache = quotaCache;
        _sql = sql;
    }

    public async Task<ApiResponse<List<ParkingAllocationDto>>> HandleAsync(GetCompanyAllocationsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<List<ParkingAllocationDto>>(false, "Access denied. You are not an active member of this company.", null);
        }

        var quotas = await _quotaCache.GetCompanyAllocationsAsync(query.CompanyId, ct);

        const string sql = """
            SELECT
                f."AllocationId" AS AllocationId,
                f."MembershipId" AS MembershipId,
                TRIM(COALESCE(u."FirstName", '') || ' ' || COALESCE(u."LastName", '')) AS UserName,
                f."SlotNumber" AS SlotNumber,
                f."AssignedAt" AS AssignedAt
            FROM "FixedSlotAssignments" f
            LEFT JOIN "UserCompanyMemberships" m ON m."Id" = f."MembershipId" AND m."IsDeleted" = FALSE
            LEFT JOIN "Users" u ON u."Id" = m."UserId" AND u."IsDeleted" = FALSE
            WHERE f."CompanyId" = @CompanyId
                AND f."IsDeleted" = FALSE
            ORDER BY f."AllocationId", f."SlotNumber";
            """;

        using var connection = _sql.CreateConnection();
        var fixedAssignmentRows = await connection.QueryAsync<FixedAssignmentRow>(
            new CommandDefinition(sql, new { query.CompanyId }, cancellationToken: ct));

        var assignmentsByAllocation = fixedAssignmentRows
            .GroupBy(r => r.AllocationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new FixedSlotAssignmentDto(r.MembershipId, r.UserName, r.SlotNumber, r.AssignedAt)).ToList());

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
            q.CreatedAt)).ToList();

        return new ApiResponse<List<ParkingAllocationDto>>(true, null, allocations);
    }
}

public class GetCompanyParkingSpacesHandler : IQueryHandler<GetCompanyParkingSpacesQuery, ApiResponse<List<CorporateParkingSpaceDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyParkingSpacesHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
    }

    public async Task<ApiResponse<List<CorporateParkingSpaceDto>>> HandleAsync(GetCompanyParkingSpacesQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<List<CorporateParkingSpaceDto>>(false, "Only company admins can view company-owned parking.", null);
        }

        const string sql = """
            SELECT
                ps."Id" AS Id,
                ps."CompanyOwnerId" AS CompanyId,
                ps."Title" AS Title,
                ps."Description" AS Description,
                ps."Address" AS Address,
                ps."City" AS City,
                ps."State" AS State,
                ps."Country" AS Country,
                ps."PostalCode" AS PostalCode,
                ps."Latitude" AS Latitude,
                ps."Longitude" AS Longitude,
                ps."ParkingType" AS ParkingType,
                ps."TotalSpots" AS TotalSpots,
                ps."AvailableSpots" AS AvailableSpots,
                ps."HourlyRate" AS HourlyRate,
                ps."DailyRate" AS DailyRate,
                ps."WeeklyRate" AS WeeklyRate,
                ps."MonthlyRate" AS MonthlyRate,
                ps."OpenTime" AS OpenTime,
                ps."CloseTime" AS CloseTime,
                ps."Is24Hours" AS Is24Hours,
                ps."Amenities" AS AmenitiesCsv,
                ps."AllowedVehicleTypes" AS AllowedVehicleTypesCsv,
                ps."ImageUrls" AS ImageUrlsCsv,
                ps."IsActive" AS IsActive,
                ps."IsVerified" AS IsVerified,
                ps."SpecialInstructions" AS SpecialInstructions,
                ps."ZoneCode" AS ZoneCode,
                ps."CreatedAt" AS CreatedAt
            FROM "ParkingSpaces" ps
            WHERE ps."CompanyOwnerId" = @CompanyId
                AND ps."OwnershipType" = @CompanyOwnedType
                AND ps."IsDeleted" = FALSE
            ORDER BY ps."CreatedAt" DESC;
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<CorporateParkingSpaceRow>(
            new CommandDefinition(
                sql,
                new { query.CompanyId, CompanyOwnedType = (int)ParkingSpaceOwnershipType.CompanyOwned },
                cancellationToken: ct));

        var spaces = rows.Select(r => new CorporateParkingSpaceDto(
            r.Id,
            r.CompanyId,
            r.Title,
            r.Description,
            r.Address,
            r.City,
            r.State,
            r.Country,
            r.PostalCode,
            r.Latitude,
            r.Longitude,
            (ParkingType)r.ParkingType,
            r.TotalSpots,
            r.AvailableSpots,
            r.HourlyRate,
            r.DailyRate,
            r.WeeklyRate,
            r.MonthlyRate,
            r.OpenTime,
            r.CloseTime,
            r.Is24Hours,
            SplitCsv(r.AmenitiesCsv),
            ParseVehicleTypes(r.AllowedVehicleTypesCsv),
            SplitCsv(r.ImageUrlsCsv),
            r.IsActive,
            r.IsVerified,
            r.SpecialInstructions,
            r.ZoneCode,
            r.CreatedAt)).ToList();

        return new ApiResponse<List<CorporateParkingSpaceDto>>(true, null, spaces);
    }

    private static List<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
    }

    private static List<VehicleType> ParseVehicleTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<VehicleType>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => Enum.TryParse<VehicleType>(v.Trim(), out var vehicleType) ? vehicleType : VehicleType.Car)
            .ToList();
    }
}

public class GetMemberBookingsHandler : IQueryHandler<GetMemberBookingsQuery, ApiResponse<MemberBookingsDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetMemberBookingsHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
    }

    public async Task<ApiResponse<MemberBookingsDto>> HandleAsync(GetMemberBookingsQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<MemberBookingsDto>(false, "Access denied.", null);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var offset = (page - 1) * pageSize;

        const string sql = """
            SELECT
                cb."Id" AS Id,
                cb."BookingId" AS BookingId,
                b."BookingReference" AS BookingReference,
                cb."SlotType" AS SlotType,
                b."SlotNumber" AS SlotNumber,
                cb."IsVisitorBooking" AS IsVisitorBooking,
                cb."VisitorName" AS VisitorName,
                cb."VisitorLicensePlate" AS VisitorLicensePlate,
                b."StartDateTime" AS StartDateTime,
                b."EndDateTime" AS EndDateTime,
                b."Status" AS BookingStatus,
                COALESCE(cb."AccessQrToken", b."QRCode") AS QrCodeToken,
                cb."CreatedAt" AS CreatedAt
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
                AND cb."MembershipId" = @MembershipId
                AND cb."IsDeleted" = FALSE
                AND b."IsDeleted" = FALSE
            ORDER BY cb."CreatedAt" DESC
            OFFSET @Offset
            LIMIT @PageSize;

            SELECT COUNT(*)
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
                AND cb."MembershipId" = @MembershipId
                AND cb."IsDeleted" = FALSE
                AND b."IsDeleted" = FALSE;
            """;

        using var connection = _sql.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                sql,
                new { query.CompanyId, MembershipId = membership.Id, Offset = offset, PageSize = pageSize },
                cancellationToken: ct));

        var rows = (await multi.ReadAsync<MemberBookingRow>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();

        var bookings = rows.Select(r => new CorporateBookingDto(
            r.Id,
            r.BookingId,
            r.BookingReference,
            (CorporateSlotType)r.SlotType,
            r.SlotNumber,
            r.IsVisitorBooking,
            r.VisitorName,
            r.VisitorLicensePlate,
            r.StartDateTime,
            r.EndDateTime,
            (BookingStatus)r.BookingStatus,
            r.QrCodeToken,
            r.CreatedAt)).ToList();

        return new ApiResponse<MemberBookingsDto>(true, null, new MemberBookingsDto(bookings, totalCount, page, pageSize));
    }
}

public class GetVendorAllocationsHandler : IQueryHandler<GetVendorAllocationsQuery, ApiResponse<List<VendorParkingAllocationDto>>>
{
    private readonly ISqlConnectionFactory _sql;

    public GetVendorAllocationsHandler(ISqlConnectionFactory sql)
    {
        _sql = sql;
    }

    public async Task<ApiResponse<List<VendorParkingAllocationDto>>> HandleAsync(GetVendorAllocationsQuery query, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                pa."Id" AS Id,
                pa."CompanyId" AS CompanyId,
                c."Name" AS CompanyName,
                pa."ParkingSpaceId" AS ParkingSpaceId,
                ps."Title" AS ParkingSpaceTitle,
                pa."TotalSlots" AS TotalSlots,
                pa."FixedSlots" AS FixedSlots,
                pa."SharedSlots" AS SharedSlots,
                pa."MonthlyRate" AS MonthlyRate,
                pa."StartDate" AS StartDate,
                pa."EndDate" AS EndDate,
                pa."Status" AS Status,
                pa."SourceType" AS SourceType,
                pa."VendorId" AS VendorId,
                pa."LeaseReference" AS LeaseReference,
                pa."ApprovedByUserId" AS ApprovedByUserId,
                pa."ApprovedAt" AS ApprovedAt,
                pa."CreatedAt" AS CreatedAt,
                pa."MaxBookingsPerDay" AS MaxBookingsPerEmployeePerDay,
                pa."MaxBookingsPerWeek" AS MaxBookingsPerEmployeePerWeek,
                pa."PriorityThreshold" AS PriorityThreshold,
                pa."AllowedStartTime" AS AllowedStartTime,
                pa."AllowedEndTime" AS AllowedEndTime,
                pa."AllowWeekends" AS AllowWeekends
            FROM "ParkingAllocations" pa
            INNER JOIN "ParkingSpaces" ps ON ps."Id" = pa."ParkingSpaceId"
            INNER JOIN "Companies" c ON c."Id" = pa."CompanyId"
            WHERE ps."OwnerId" = @VendorId
                AND pa."IsDeleted" = FALSE
                AND ps."IsDeleted" = FALSE
                AND c."IsDeleted" = FALSE
            ORDER BY
                CASE WHEN pa."Status" = @PendingStatus THEN 0 ELSE 1 END,
                pa."CreatedAt" DESC;
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<VendorAllocationRow>(
            new CommandDefinition(
                sql,
                new { query.VendorId, PendingStatus = (int)AllocationStatus.PendingApproval },
                cancellationToken: ct));

        var allocations = rows.Select(r => new VendorParkingAllocationDto(
            r.Id,
            r.CompanyId,
            r.CompanyName,
            r.ParkingSpaceId,
            r.ParkingSpaceTitle,
            r.TotalSlots,
            r.FixedSlots,
            r.SharedSlots,
            r.MonthlyRate,
            r.StartDate,
            r.EndDate,
            (AllocationStatus)r.Status,
            (ParkingAllocationSource)r.SourceType,
            r.VendorId,
            r.LeaseReference,
            r.ApprovedByUserId,
            r.ApprovedAt,
            new BookingPolicyDto(
                r.MaxBookingsPerEmployeePerDay,
                r.MaxBookingsPerEmployeePerWeek,
                r.PriorityThreshold,
                r.AllowedStartTime,
                r.AllowedEndTime,
                r.AllowWeekends),
            r.CreatedAt)).ToList();

        return new ApiResponse<List<VendorParkingAllocationDto>>(true, null, allocations);
    }
}

public class GetCompanyWaitlistHandler : IQueryHandler<GetCompanyWaitlistQuery, ApiResponse<List<CorporateWaitlistDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyWaitlistHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
    }

    public async Task<ApiResponse<List<CorporateWaitlistDto>>> HandleAsync(GetCompanyWaitlistQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<List<CorporateWaitlistDto>>(false, "Access denied.", null);
        }

        var onlyOwnEntries = !membership.IsAdmin;

        const string sql = """
            SELECT
                cw."Id" AS Id,
                cw."AllocationId" AS AllocationId,
                cw."IsVisitorBooking" AS IsVisitorBooking,
                cw."RequestedStartDateTime" AS RequestedStartDateTime,
                cw."RequestedEndDateTime" AS RequestedEndDateTime,
                cw."VehicleNumber" AS VehicleNumber,
                cw."VisitorName" AS VisitorName,
                cw."VisitorLicensePlate" AS VisitorLicensePlate,
                cw."Status" AS Status,
                cw."PriorityAtRequest" AS PriorityAtRequest,
                cw."CreatedAt" AS CreatedAt,
                CASE
                    WHEN cw."Status" = @PendingStatus THEN (
                        SELECT COUNT(*) + 1
                        FROM "CorporateWaitlistEntries" other
                        WHERE other."CompanyId" = cw."CompanyId"
                            AND other."AllocationId" = cw."AllocationId"
                            AND other."Status" = @PendingStatus
                            AND other."IsDeleted" = FALSE
                            AND other."RequestedStartDateTime" < cw."RequestedEndDateTime"
                            AND other."RequestedEndDateTime" > cw."RequestedStartDateTime"
                            AND (
                                other."PriorityAtRequest" > cw."PriorityAtRequest"
                                OR (
                                    other."PriorityAtRequest" = cw."PriorityAtRequest"
                                    AND other."CreatedAt" < cw."CreatedAt"
                                )
                            )
                    )
                    ELSE 0
                END AS Position
            FROM "CorporateWaitlistEntries" cw
            WHERE cw."CompanyId" = @CompanyId
                AND cw."IsDeleted" = FALSE
                AND (@OnlyOwnEntries = FALSE OR cw."MembershipId" = @MembershipId)
            ORDER BY cw."Status", cw."PriorityAtRequest" DESC, cw."CreatedAt";
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<WaitlistRow>(
            new CommandDefinition(
                sql,
                new
                {
                    query.CompanyId,
                    MembershipId = membership.Id,
                    OnlyOwnEntries = onlyOwnEntries,
                    PendingStatus = (int)WaitlistStatus.Pending
                },
                cancellationToken: ct));

        var waitlist = rows.Select(r => new CorporateWaitlistDto(
            r.Id,
            r.AllocationId,
            r.IsVisitorBooking,
            r.RequestedStartDateTime,
            r.RequestedEndDateTime,
            r.VehicleNumber,
            r.VisitorName,
            r.VisitorLicensePlate,
            (WaitlistStatus)r.Status,
            r.PriorityAtRequest,
            r.Position,
            r.CreatedAt)).ToList();

        return new ApiResponse<List<CorporateWaitlistDto>>(true, null, waitlist);
    }
}

public class GetCompanyDashboardHandler : IQueryHandler<GetCompanyDashboardQuery, ApiResponse<CompanyDashboardDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISqlConnectionFactory _sql;

    public GetCompanyDashboardHandler(IUnitOfWork uow, ISqlConnectionFactory sql)
    {
        _uow = uow;
        _sql = sql;
    }

    public async Task<ApiResponse<CompanyDashboardDto>> HandleAsync(GetCompanyDashboardQuery query, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(query.CompanyId, query.UserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CompanyDashboardDto>(false, "Only company admins can view the dashboard.", null);
        }

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);
        var sevenDayStart = todayStart.AddDays(-6);

        const string sql = """
            SELECT
                COALESCE((
                    SELECT COUNT(*)
                    FROM "UserCompanyMemberships"
                    WHERE "CompanyId" = @CompanyId AND "IsDeleted" = FALSE
                ), 0) AS TotalMembers,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "UserCompanyMemberships"
                    WHERE "CompanyId" = @CompanyId AND "IsDeleted" = FALSE AND "IsActive" = TRUE
                ), 0) AS ActiveMembers,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "ParkingAllocations"
                    WHERE "CompanyId" = @CompanyId AND "IsDeleted" = FALSE
                ), 0) AS TotalAllocations,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "ParkingAllocations"
                    WHERE "CompanyId" = @CompanyId AND "IsDeleted" = FALSE AND "Status" = @ActiveStatus
                ), 0) AS ActiveAllocations,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "ParkingSpaces"
                    WHERE "CompanyOwnerId" = @CompanyId
                        AND "OwnershipType" = @CompanyOwnedType
                        AND "IsDeleted" = FALSE
                ), 0) AS OwnedParkingSpaces,
                COALESCE((
                    SELECT SUM("TotalSpots")
                    FROM "ParkingSpaces"
                    WHERE "CompanyOwnerId" = @CompanyId
                        AND "OwnershipType" = @CompanyOwnedType
                        AND "IsDeleted" = FALSE
                ), 0) AS OwnedParkingSlots,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "ParkingAllocations"
                    WHERE "CompanyId" = @CompanyId
                        AND "IsDeleted" = FALSE
                        AND "SourceType" = @VendorLeaseSource
                ), 0) AS LeasedAllocations,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "ParkingAllocations"
                    WHERE "CompanyId" = @CompanyId
                        AND "IsDeleted" = FALSE
                        AND "SourceType" = @VendorLeaseSource
                        AND "Status" = @PendingAllocationStatus
                ), 0) AS PendingVendorAllocations,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                        AND cb."IsDeleted" = FALSE
                        AND b."IsDeleted" = FALSE
                        AND b."StartDateTime" >= @MonthStart
                        AND b."StartDateTime" < @MonthEnd
                ), 0) AS TotalBookingsThisMonth,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                        AND cb."IsDeleted" = FALSE
                        AND cb."IsVisitorBooking" = TRUE
                        AND b."IsDeleted" = FALSE
                        AND b."StartDateTime" >= @MonthStart
                        AND b."StartDateTime" < @MonthEnd
                ), 0) AS VisitorBookingsThisMonth,
                COALESCE((
                    SELECT SUM(EXTRACT(EPOCH FROM (b."EndDateTime" - b."StartDateTime")) / 3600.0)::numeric
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                        AND cb."IsDeleted" = FALSE
                        AND b."IsDeleted" = FALSE
                        AND b."StartDateTime" >= @MonthStart
                        AND b."StartDateTime" < @MonthEnd
                ), 0) AS TotalHoursUsedThisMonth,
                COALESCE((
                    SELECT SUM(pa."MonthlyRate")
                    FROM "ParkingAllocations" pa
                    WHERE pa."CompanyId" = @CompanyId
                        AND pa."IsDeleted" = FALSE
                        AND pa."Status" = @ActiveStatus
                ), 0) AS MonthlySpend,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "CorporateBookings" cb
                    INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                    WHERE cb."CompanyId" = @CompanyId
                        AND cb."IsDeleted" = FALSE
                        AND b."IsDeleted" = FALSE
                        AND b."StartDateTime" < @TodayEnd
                        AND b."EndDateTime" > @TodayStart
                ), 0) AS UsedSlotsToday,
                COALESCE((
                    SELECT SUM(pa."TotalSlots")
                    FROM "ParkingAllocations" pa
                    WHERE pa."CompanyId" = @CompanyId
                        AND pa."IsDeleted" = FALSE
                        AND pa."Status" = @ActiveStatus
                ), 0) AS TotalSlotsToday,
                COALESCE((
                    SELECT COUNT(*)
                    FROM "CorporateWaitlistEntries" cw
                    WHERE cw."CompanyId" = @CompanyId
                        AND cw."IsDeleted" = FALSE
                        AND cw."Status" = @PendingWaitlistStatus
                ), 0) AS ActiveWaitlistEntries,
                COALESCE((
                    SELECT COUNT(*)
                    FROM (
                        SELECT cb1."MembershipId"
                        FROM "CorporateBookings" cb1
                        INNER JOIN "Bookings" b1 ON b1."Id" = cb1."BookingId"
                        INNER JOIN "CorporateBookings" cb2
                            ON cb2."CompanyId" = cb1."CompanyId"
                            AND cb2."MembershipId" = cb1."MembershipId"
                            AND cb2."Id" > cb1."Id"
                        INNER JOIN "Bookings" b2 ON b2."Id" = cb2."BookingId"
                        WHERE cb1."CompanyId" = @CompanyId
                            AND cb1."IsDeleted" = FALSE
                            AND cb2."IsDeleted" = FALSE
                            AND b1."IsDeleted" = FALSE
                            AND b2."IsDeleted" = FALSE
                            AND b1."Status" NOT IN (@CancelledStatus, @RejectedStatus, @ExpiredStatus)
                            AND b2."Status" NOT IN (@CancelledStatus, @RejectedStatus, @ExpiredStatus)
                            AND b1."StartDateTime" < b2."EndDateTime"
                            AND b1."EndDateTime" > b2."StartDateTime"
                        GROUP BY cb1."MembershipId"
                    ) suspicious
                ), 0) AS SuspiciousActivityCount;

            SELECT
                day::date AS Day,
                COALESCE(COUNT(cb."Id"), 0) AS BookingCount
            FROM generate_series(@SevenDayStart::date, @TodayStart::date, interval '1 day') AS day
            LEFT JOIN "Bookings" b
                ON b."StartDateTime" >= day
                AND b."StartDateTime" < day + interval '1 day'
                AND b."IsDeleted" = FALSE
            LEFT JOIN "CorporateBookings" cb
                ON cb."BookingId" = b."Id"
                AND cb."CompanyId" = @CompanyId
                AND cb."IsDeleted" = FALSE
            GROUP BY day
            ORDER BY day;

            SELECT
                pa."Id" AS AllocationId,
                ps."Title" AS ParkingSpaceTitle,
                pa."TotalSlots" AS TotalSlots,
                COALESCE(COUNT(b."Id"), 0) AS UsedToday
            FROM "ParkingAllocations" pa
            INNER JOIN "ParkingSpaces" ps ON ps."Id" = pa."ParkingSpaceId"
            LEFT JOIN "CorporateBookings" cb
                ON cb."AllocationId" = pa."Id"
                AND cb."CompanyId" = @CompanyId
                AND cb."IsDeleted" = FALSE
            LEFT JOIN "Bookings" b
                ON b."Id" = cb."BookingId"
                AND b."IsDeleted" = FALSE
                AND b."StartDateTime" < @TodayEnd
                AND b."EndDateTime" > @TodayStart
            WHERE pa."CompanyId" = @CompanyId
                AND pa."IsDeleted" = FALSE
                AND pa."Status" = @ActiveStatus
            GROUP BY pa."Id", ps."Title", pa."TotalSlots"
            ORDER BY ps."Title";

            SELECT
                EXTRACT(HOUR FROM b."StartDateTime")::int AS HourOfDay,
                COUNT(*) AS BookingCount
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
                AND cb."IsDeleted" = FALSE
                AND b."IsDeleted" = FALSE
                AND b."StartDateTime" >= @MonthStart
                AND b."StartDateTime" < @MonthEnd
            GROUP BY EXTRACT(HOUR FROM b."StartDateTime")
            ORDER BY BookingCount DESC, HourOfDay
            LIMIT 5;

            WITH active_bookings AS (
                SELECT
                    cb."MembershipId" AS membershipid,
                    b."Id" AS bookingid,
                    b."StartDateTime" AS startdatetime,
                    b."EndDateTime" AS enddatetime
                FROM "CorporateBookings" cb
                INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
                WHERE cb."CompanyId" = @CompanyId
                    AND cb."IsDeleted" = FALSE
                    AND b."IsDeleted" = FALSE
                    AND b."Status" NOT IN (@CancelledStatus, @RejectedStatus, @ExpiredStatus)
                    AND b."StartDateTime" >= @MonthStart
                    AND b."StartDateTime" < @MonthEnd
            ),
            overlap_counts AS (
                SELECT
                    a.membershipid AS membershipid,
                    COUNT(*) AS overlappingbookingpairs
                FROM active_bookings a
                INNER JOIN active_bookings b
                    ON a.membershipid = b.membershipid
                    AND a.bookingid < b.bookingid
                    AND a.startdatetime < b.enddatetime
                    AND a.enddatetime > b.startdatetime
                GROUP BY a.membershipid
            )
            SELECT
                oc.membershipid AS MembershipId,
                TRIM(COALESCE(u."FirstName", '') || ' ' || COALESCE(u."LastName", '')) AS UserName,
                m."Priority" AS Priority,
                oc.overlappingbookingpairs AS OverlappingBookingPairs,
                (oc.overlappingbookingpairs * 10) + m."Priority" AS RiskScore
            FROM overlap_counts oc
            INNER JOIN "UserCompanyMemberships" m ON m."Id" = oc.membershipid AND m."IsDeleted" = FALSE
            INNER JOIN "Users" u ON u."Id" = m."UserId" AND u."IsDeleted" = FALSE
            ORDER BY RiskScore DESC, UserName
            LIMIT 5;
            """;

        using var connection = _sql.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(
                sql,
                new
                {
                    query.CompanyId,
                    ActiveStatus = (int)AllocationStatus.Active,
                    PendingAllocationStatus = (int)AllocationStatus.PendingApproval,
                    CompanyOwnedType = (int)ParkingSpaceOwnershipType.CompanyOwned,
                    VendorLeaseSource = (int)ParkingAllocationSource.VendorLease,
                    PendingWaitlistStatus = (int)WaitlistStatus.Pending,
                    CancelledStatus = (int)BookingStatus.Cancelled,
                    RejectedStatus = (int)BookingStatus.Rejected,
                    ExpiredStatus = (int)BookingStatus.Expired,
                    MonthStart = monthStart,
                    MonthEnd = monthEnd,
                    TodayStart = todayStart,
                    TodayEnd = todayEnd,
                    SevenDayStart = sevenDayStart
                },
                cancellationToken: ct));

        var summary = await multi.ReadSingleAsync<DashboardSummaryRow>();
        var dailyRows = (await multi.ReadAsync<DashboardDayRow>()).ToList();
        var allocationRows = (await multi.ReadAsync<AllocationUtilizationRow>()).ToList();
        var peakHourRows = (await multi.ReadAsync<PeakHourRow>()).ToList();
        var fraudRows = (await multi.ReadAsync<FraudAlertRow>()).ToList();

        var utilizationPercent = summary.TotalSlotsToday > 0
            ? Math.Round((double)summary.UsedSlotsToday / summary.TotalSlotsToday * 100, 1)
            : 0;

        var bookingsByDay = dailyRows
            .Select(r => new DashboardChartDataDto(r.Day.ToString("ddd"), 0, r.BookingCount))
            .ToList();

        var allocationBreakdown = allocationRows
            .Select(r =>
            {
                var util = r.TotalSlots > 0
                    ? Math.Round((double)r.UsedToday / r.TotalSlots * 100, 1)
                    : 0;

                return new AllocationUtilizationDto(r.AllocationId, r.ParkingSpaceTitle, r.TotalSlots, r.UsedToday, util);
            })
            .ToList();

        var peakHours = peakHourRows
            .Select(r => new PeakHourDto(r.HourOfDay, r.BookingCount))
            .ToList();

        var fraudAlerts = fraudRows
            .Select(r => new FraudAlertDto(r.MembershipId, r.UserName, r.Priority, r.OverlappingBookingPairs, r.RiskScore))
            .ToList();

        var dashboard = new CompanyDashboardDto(
            summary.TotalMembers,
            summary.ActiveMembers,
            summary.TotalAllocations,
            summary.ActiveAllocations,
            summary.OwnedParkingSpaces,
            summary.OwnedParkingSlots,
            summary.LeasedAllocations,
            summary.PendingVendorAllocations,
            summary.TotalBookingsThisMonth,
            summary.VisitorBookingsThisMonth,
            Math.Round(summary.TotalHoursUsedThisMonth, 2),
            summary.MonthlySpend,
            utilizationPercent,
            bookingsByDay,
            allocationBreakdown,
            summary.ActiveWaitlistEntries,
            summary.SuspiciousActivityCount,
            peakHours,
            fraudAlerts);

        return new ApiResponse<CompanyDashboardDto>(true, null, dashboard);
    }
}

internal sealed class CompanyDetailsRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;
    public int BillingType { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public int ActiveAllocationCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal class MembershipRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int Role { get; set; }
    public string? EmployeeCode { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class FixedAssignmentRow
{
    public Guid AllocationId { get; set; }
    public Guid MembershipId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int SlotNumber { get; set; }
    public DateTime AssignedAt { get; set; }
}

internal sealed class MemberBookingRow
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string? BookingReference { get; set; }
    public int SlotType { get; set; }
    public int? SlotNumber { get; set; }
    public bool IsVisitorBooking { get; set; }
    public string? VisitorName { get; set; }
    public string? VisitorLicensePlate { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public int BookingStatus { get; set; }
    public string? QrCodeToken { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class CorporateParkingSpaceRow
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ParkingType { get; set; }
    public int TotalSpots { get; set; }
    public int AvailableSpots { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal WeeklyRate { get; set; }
    public decimal MonthlyRate { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public bool Is24Hours { get; set; }
    public string? AmenitiesCsv { get; set; }
    public string? AllowedVehicleTypesCsv { get; set; }
    public string? ImageUrlsCsv { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? ZoneCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class VendorAllocationRow
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid ParkingSpaceId { get; set; }
    public string ParkingSpaceTitle { get; set; } = string.Empty;
    public int TotalSlots { get; set; }
    public int FixedSlots { get; set; }
    public int SharedSlots { get; set; }
    public decimal MonthlyRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Status { get; set; }
    public int SourceType { get; set; }
    public Guid? VendorId { get; set; }
    public string? LeaseReference { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int MaxBookingsPerEmployeePerDay { get; set; }
    public int MaxBookingsPerEmployeePerWeek { get; set; }
    public int PriorityThreshold { get; set; }
    public TimeSpan AllowedStartTime { get; set; }
    public TimeSpan AllowedEndTime { get; set; }
    public bool AllowWeekends { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class WaitlistRow
{
    public Guid Id { get; set; }
    public Guid AllocationId { get; set; }
    public bool IsVisitorBooking { get; set; }
    public DateTime RequestedStartDateTime { get; set; }
    public DateTime RequestedEndDateTime { get; set; }
    public string? VehicleNumber { get; set; }
    public string? VisitorName { get; set; }
    public string? VisitorLicensePlate { get; set; }
    public int Status { get; set; }
    public int PriorityAtRequest { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class DashboardSummaryRow
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int TotalAllocations { get; set; }
    public int ActiveAllocations { get; set; }
    public int OwnedParkingSpaces { get; set; }
    public int OwnedParkingSlots { get; set; }
    public int LeasedAllocations { get; set; }
    public int PendingVendorAllocations { get; set; }
    public int TotalBookingsThisMonth { get; set; }
    public int VisitorBookingsThisMonth { get; set; }
    public decimal TotalHoursUsedThisMonth { get; set; }
    public decimal MonthlySpend { get; set; }
    public int UsedSlotsToday { get; set; }
    public int TotalSlotsToday { get; set; }
    public int ActiveWaitlistEntries { get; set; }
    public int SuspiciousActivityCount { get; set; }
}

internal sealed class DashboardDayRow
{
    public DateTime Day { get; set; }
    public int BookingCount { get; set; }
}

internal sealed class AllocationUtilizationRow
{
    public Guid AllocationId { get; set; }
    public string ParkingSpaceTitle { get; set; } = string.Empty;
    public int TotalSlots { get; set; }
    public int UsedToday { get; set; }
}

internal sealed class PeakHourRow
{
    public int HourOfDay { get; set; }
    public int BookingCount { get; set; }
}

internal sealed class FraudAlertRow
{
    public Guid MembershipId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int OverlappingBookingPairs { get; set; }
    public int RiskScore { get; set; }
}

public class GetMyCompaniesHandler : IQueryHandler<GetMyCompaniesQuery, ApiResponse<List<CompanyDto>>>
{
    private readonly ISqlConnectionFactory _sql;

    public GetMyCompaniesHandler(ISqlConnectionFactory sql)
    {
        _sql = sql;
    }

    public async Task<ApiResponse<List<CompanyDto>>> HandleAsync(GetMyCompaniesQuery query, CancellationToken ct = default)
    {
        using var connection = _sql.CreateConnection();
        var sql = @"
            SELECT
                c.""Id"" AS Id, c.""Name"" AS Name, c.""RegistrationNumber"" AS RegistrationNumber, c.""ContactEmail"" AS ContactEmail,
                c.""ContactPhone"" AS ContactPhone, c.""BillingAddress"" AS BillingAddress, c.""BillingType"" AS BillingType, c.""IsActive"" AS IsActive,
                CAST((SELECT COUNT(*) FROM ""UserCompanyMemberships"" m2 WHERE m2.""CompanyId"" = c.""Id"" AND m2.""IsDeleted"" = FALSE) AS INTEGER) AS MemberCount,
                CAST((SELECT COUNT(*) FROM ""ParkingAllocations"" pa WHERE pa.""CompanyId"" = c.""Id"" AND pa.""IsDeleted"" = FALSE) AS INTEGER) AS ActiveAllocationCount,
                c.""CreatedAt"" AS CreatedAt
            FROM ""Companies"" c
            INNER JOIN ""UserCompanyMemberships"" m ON m.""CompanyId"" = c.""Id""
            WHERE m.""UserId"" = @UserId 
              AND m.""IsDeleted"" = FALSE 
              AND m.""IsActive"" = TRUE
              AND c.""IsDeleted"" = FALSE";
              
        var companies = await connection.QueryAsync<CompanyDto>(sql, new { query.UserId });
        return new ApiResponse<List<CompanyDto>>(true, null, companies.ToList());
    }
}
