using System.Text;
using Dapper;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;




using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Infrastructure.ReadModel.Bookings;

internal sealed class BookingReadStore : IBookingReadStore
{
    private readonly ISqlConnectionFactory _sql;

    public BookingReadStore(ISqlConnectionFactory sql)
    {
        _sql = sql;
    }

    public Task<BookingListResultDto> GetUserBookingsAsync(
        Guid userId,
        BookingFilterDto? filter,
        CancellationToken ct = default)
    {
        var (page, pageSize, offset) = NormalizePaging(filter, defaultPageSize: 10);
        return QueryPagedAsync(
            baseWhere: """b."UserId" = @UserId AND b."IsDeleted" = FALSE""",
            extraJoins: null,
            parameters: new DynamicParameters(new { UserId = userId }),
            filter,
            page,
            pageSize,
            offset,
            ct);
    }

    public Task<BookingListResultDto> GetVendorBookingsAsync(
        Guid vendorId,
        BookingFilterDto? filter,
        CancellationToken ct = default)
    {
        var (page, pageSize, offset) = NormalizePaging(filter, defaultPageSize: 10);
        return QueryPagedAsync(
            baseWhere: """ps."OwnerId" = @VendorId AND b."IsDeleted" = FALSE AND ps."IsDeleted" = FALSE""",
            extraJoins: null,
            parameters: new DynamicParameters(new { VendorId = vendorId }),
            filter,
            page,
            pageSize,
            offset,
            ct);
    }

    public Task<BookingListResultDto> GetByParkingSpaceAsync(
        Guid parkingSpaceId,
        BookingFilterDto? filter,
        CancellationToken ct = default)
    {
        var (page, pageSize, offset) = NormalizePaging(filter, defaultPageSize: 20);
        return QueryPagedAsync(
            baseWhere: """b."ParkingSpaceId" = @ParkingSpaceId AND b."IsDeleted" = FALSE""",
            extraJoins: null,
            parameters: new DynamicParameters(new { ParkingSpaceId = parkingSpaceId }),
            filter,
            page,
            pageSize,
            offset,
            ct);
    }

    public async Task<int> CountPendingForVendorAsync(Guid vendorId, CancellationToken ct = default)
    {
        // Parity with previous handler: Pending + PendingExtension
        const string sql = """
            SELECT COUNT(*)
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON ps."Id" = b."ParkingSpaceId"
            WHERE ps."OwnerId" = @VendorId
              AND b."IsDeleted" = FALSE
              AND ps."IsDeleted" = FALSE
              AND b."Status" IN (@PendingStatus, @PendingExtensionStatus)
            """;

        using var connection = _sql.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    VendorId = vendorId,
                    PendingStatus = (int)BookingStatus.Pending,
                    PendingExtensionStatus = (int)BookingStatus.PendingExtension
                },
                cancellationToken: ct));
    }

    private async Task<BookingListResultDto> QueryPagedAsync(
        string baseWhere,
        string? extraJoins,
        DynamicParameters parameters,
        BookingFilterDto? filter,
        int page,
        int pageSize,
        int offset,
        CancellationToken ct)
    {
        var filterSql = new StringBuilder();
        if (filter?.Status.HasValue == true)
        {
            filterSql.Append(""" AND b."Status" = @Status""");
            parameters.Add("Status", (int)filter.Status.Value);
        }
        if (filter?.StartDate.HasValue == true)
        {
            filterSql.Append(""" AND b."StartDateTime" >= @StartDate""");
            parameters.Add("StartDate", filter.StartDate.Value);
        }
        if (filter?.EndDate.HasValue == true)
        {
            filterSql.Append(""" AND b."EndDateTime" <= @EndDate""");
            parameters.Add("EndDate", filter.EndDate.Value);
        }

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var joins = $"""
            INNER JOIN "ParkingSpaces" ps ON ps."Id" = b."ParkingSpaceId"
            INNER JOIN "Users" u ON u."Id" = b."UserId"
            LEFT JOIN "Payments" pay ON pay."BookingId" = b."Id" AND pay."IsDeleted" = FALSE
            LEFT JOIN "ParkingPasses" pp ON pp."Id" = b."ParkingPassId" AND pp."IsDeleted" = FALSE
            {extraJoins}
            """;

        var selectList = $"""
            SELECT
                b."Id" AS Id,
                b."UserId" AS UserId,
                TRIM(COALESCE(u."FirstName", '') || ' ' || COALESCE(u."LastName", '')) AS UserName,
                b."ParkingSpaceId" AS ParkingSpaceId,
                ps."Title" AS ParkingSpaceTitle,
                ps."Address" AS ParkingSpaceAddress,
                ps."Latitude" AS Latitude,
                ps."Longitude" AS Longitude,
                b."StartDateTime" AS StartDateTime,
                b."EndDateTime" AS EndDateTime,
                b."PricingType" AS PricingType,
                b."VehicleType" AS VehicleType,
                b."SlotNumber" AS SlotNumber,
                b."VehicleNumber" AS VehicleNumber,
                b."VehicleModel" AS VehicleModel,
                b."VehicleColor" AS VehicleColor,
                b."BaseAmount" AS BaseAmount,
                b."TaxAmount" AS TaxAmount,
                b."ServiceFee" AS ServiceFee,
                b."DiscountAmount" AS DiscountAmount,
                b."TotalAmount" AS TotalAmount,
                b."DiscountCode" AS DiscountCode,
                b."Status" AS Status,
                b."BookingReference" AS BookingReference,
                b."CheckInTime" AS CheckInTime,
                b."CheckOutTime" AS CheckOutTime,
                pay."Status" AS PaymentStatus,
                b."CreatedAt" AS CreatedAt,
                b."PendingExtensionEndDateTime" AS PendingExtensionEndDateTime,
                b."PendingExtensionAmount" AS PendingExtensionAmount,
                (b."PendingExtensionEndDateTime" IS NOT NULL) AS HasPendingExtension,
                b."ParkingPassId" AS ParkingPassId,
                CASE pp."PassType"
                    WHEN 0 THEN 'Monthly'
                    WHEN 1 THEN 'Weekly'
                    WHEN 2 THEN 'Corporate'
                    ELSE NULL
                END AS ParkingPassType,
                (b."ParkingPassId" IS NOT NULL) AS IsPassApplied,
                b."QRCode" AS QrCode,
                b."FacilityLevel" AS FacilityLevel,
                b."FacilityZone" AS FacilityZone,
                b."BayLabel" AS BayLabel,
                b."ValetStatus" AS ValetStatus,
                b."ValetRequestedAt" AS ValetRequestedAt,
                b."ValetTargetReadyAt" AS ValetTargetReadyAt,
                b."ValetReadyAt" AS ValetReadyAt,
                b."ValetNotes" AS ValetNotes,
                COALESCE(ps."IsValetEnabled", FALSE) AS IsValetEnabled,
                COALESCE(ps."IsBayGuidanceEnabled", FALSE) AS IsBayGuidanceEnabled,
                ps."IndoorGuidanceNotes" AS IndoorGuidanceNotes,
                COALESCE((
                    SELECT SUM(l."UnitPrice" * l."Quantity")
                    FROM "BookingAncillaryLines" l
                    WHERE l."BookingId" = b."Id" AND l."IsDeleted" = FALSE
                ), 0) AS AncillarySubtotal
            FROM "Bookings" b
            {joins}
            WHERE {baseWhere}{filterSql}
            ORDER BY b."CreatedAt" DESC
            OFFSET @Offset LIMIT @PageSize;

            SELECT COUNT(*)
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON ps."Id" = b."ParkingSpaceId"
            WHERE {baseWhere}{filterSql};
            """;

        using var connection = _sql.CreateConnection();
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(selectList, parameters, cancellationToken: ct));

        var items = (await multi.ReadAsync<BookingDto>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

        if (items.Count > 0)
        {
            var bookingIds = items.Select(i => i.Id).ToArray();
            const string linesSql = """
                SELECT
                    l."Id" AS Id,
                    l."BookingId" AS BookingId,
                    l."ServiceId" AS ServiceId,
                    l."SnapshotName" AS SnapshotName,
                    l."UnitPrice" AS UnitPrice,
                    l."Quantity" AS Quantity,
                    (l."UnitPrice" * l."Quantity") AS LineTotal
                FROM "BookingAncillaryLines" l
                WHERE l."IsDeleted" = FALSE
                  AND l."BookingId" = ANY(@BookingIds)
                ORDER BY l."CreatedAt"
                """;

            var lineRows = (await connection.QueryAsync<AncillaryLineRow>(
                new CommandDefinition(linesSql, new { BookingIds = bookingIds }, cancellationToken: ct))).ToList();

            if (lineRows.Count > 0)
            {
                var byBooking = lineRows
                    .GroupBy(r => r.BookingId)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<BookingAncillaryLineDto>)g
                            .Select(r => new BookingAncillaryLineDto(
                                r.Id, r.ServiceId, r.SnapshotName, r.UnitPrice, r.Quantity, r.LineTotal))
                            .ToList());

                items = items
                    .Select(b => byBooking.TryGetValue(b.Id, out var lines)
                        ? b with { AncillaryLines = lines }
                        : b)
                    .ToList();
            }
        }

        return new BookingListResultDto(items, totalCount, page, pageSize, totalPages);
    }

    private sealed class AncillaryLineRow
    {
        public Guid Id { get; init; }
        public Guid BookingId { get; init; }
        public Guid? ServiceId { get; init; }
        public string SnapshotName { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public int Quantity { get; init; }
        public decimal LineTotal { get; init; }
    }

    private static (int Page, int PageSize, int Offset) NormalizePaging(BookingFilterDto? filter, int defaultPageSize)
    {
        var page = Math.Max(1, filter?.Page ?? 1);
        var pageSize = Math.Clamp(filter?.PageSize > 0 ? filter.PageSize : defaultPageSize, 1, 200);
        return (page, pageSize, (page - 1) * pageSize);
    }
}

