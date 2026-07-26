using Dapper;
using ParkingApp.Admin.Application.DTOs;
using ParkingApp.Admin.Application.Interfaces;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Admin.Infrastructure.ReadStores;

/// <summary>
/// Platform admin ops reads (AD-015 intentional cross-table exception for support console).
/// Tables touched: Users, ParkingSpaces, Bookings, Payments, Companies, AdminActionLogs.
/// </summary>
internal sealed class AdminReadStore : IAdminReadStore
{
    private readonly ISqlConnectionFactory _sql;

    public AdminReadStore(ISqlConnectionFactory sql) => _sql = sql;

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
              (SELECT COUNT(*)::int FROM "Users" WHERE "IsDeleted" = FALSE) AS "TotalUsers",
              (SELECT COUNT(*)::int FROM "Users" WHERE "IsDeleted" = FALSE AND "IsActive" = TRUE) AS "ActiveUsers",
              (SELECT COUNT(*)::int FROM "Users" WHERE "IsDeleted" = FALSE AND "Role" = 0) AS "AdminUsers",
              (SELECT COUNT(*)::int FROM "ParkingSpaces" WHERE "IsDeleted" = FALSE) AS "TotalListings",
              (SELECT COUNT(*)::int FROM "ParkingSpaces" WHERE "IsDeleted" = FALSE AND "IsActive" = TRUE) AS "ActiveListings",
              (SELECT COUNT(*)::int FROM "Bookings" WHERE "IsDeleted" = FALSE) AS "TotalBookings",
              (SELECT COUNT(*)::int FROM "Bookings" WHERE "IsDeleted" = FALSE AND "Status" IN (0, 1, 2, 6, 8, 9)) AS "ActiveBookings",
              (SELECT COUNT(*)::int FROM "Bookings" WHERE "IsDeleted" = FALSE AND "Status" = 3) AS "CompletedBookings",
              (SELECT COUNT(*)::int FROM "Payments" WHERE "IsDeleted" = FALSE) AS "TotalPayments",
              (SELECT COALESCE(SUM("Amount"), 0) FROM "Payments" WHERE "IsDeleted" = FALSE AND "Status" IN (1, 4)) AS "TotalPaymentVolume",
              (SELECT COALESCE(SUM("RefundAmount"), 0) FROM "Payments" WHERE "IsDeleted" = FALSE AND "RefundAmount" IS NOT NULL) AS "RefundedVolume",
              (SELECT COUNT(*)::int FROM "Companies" WHERE "IsDeleted" = FALSE) AS "Companies",
              (SELECT COUNT(*)::int FROM "AdminActionLogs" WHERE "OccurredAtUtc" >= @SinceUtc) AS "AuditEventsLast7Days"
            """;

        using var connection = _sql.CreateConnection();
        var row = await connection.QuerySingleAsync<DashboardRow>(
            new CommandDefinition(
                sql,
                new { SinceUtc = DateTime.UtcNow.AddDays(-7) },
                cancellationToken: cancellationToken));

        return new AdminDashboardDto(
            row.TotalUsers,
            row.ActiveUsers,
            row.AdminUsers,
            row.TotalListings,
            row.ActiveListings,
            row.TotalBookings,
            row.ActiveBookings,
            row.CompletedBookings,
            row.TotalPayments,
            row.TotalPaymentVolume,
            row.RefundedVolume,
            row.Companies,
            row.AuditEventsLast7Days,
            DateTime.UtcNow);
    }

    public async Task<AdminAuditLogPageDto> GetAuditLogsAsync(
        string? action,
        string? resourceType,
        Guid? actorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(action))
            where.Add("""a."Action" ILIKE @Action""");
        if (!string.IsNullOrWhiteSpace(resourceType))
            where.Add("""a."ResourceType" = @ResourceType""");
        if (actorUserId.HasValue)
            where.Add("""a."ActorUserId" = @ActorUserId""");

        var whereSql = where.Count == 0 ? "TRUE" : string.Join(" AND ", where);
        var offset = (page - 1) * pageSize;

        var countSql = $"""SELECT COUNT(*)::int FROM "AdminActionLogs" a WHERE {whereSql}""";
        var listSql = $"""
            SELECT
              a."Id" AS Id,
              a."OccurredAtUtc" AS OccurredAtUtc,
              a."ActorUserId" AS ActorUserId,
              a."ActorEmail" AS ActorEmail,
              a."Action" AS Action,
              a."ResourceType" AS ResourceType,
              a."ResourceId" AS ResourceId,
              a."PayloadJson" AS PayloadJson
            FROM "AdminActionLogs" a
            WHERE {whereSql}
            ORDER BY a."OccurredAtUtc" DESC
            OFFSET @Offset LIMIT @PageSize
            """;

        var args = new
        {
            Action = string.IsNullOrWhiteSpace(action) ? null : $"%{action.Trim()}%",
            ResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType.Trim(),
            ActorUserId = actorUserId,
            Offset = offset,
            PageSize = pageSize
        };

        using var connection = _sql.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, args, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<AdminAuditLogListItemDto>(
            new CommandDefinition(listSql, args, cancellationToken: cancellationToken))).ToList();

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new AdminAuditLogPageDto(items, total, page, pageSize, totalPages);
    }

    private sealed class DashboardRow
    {
        public int TotalUsers { get; init; }
        public int ActiveUsers { get; init; }
        public int AdminUsers { get; init; }
        public int TotalListings { get; init; }
        public int ActiveListings { get; init; }
        public int TotalBookings { get; init; }
        public int ActiveBookings { get; init; }
        public int CompletedBookings { get; init; }
        public int TotalPayments { get; init; }
        public decimal TotalPaymentVolume { get; init; }
        public decimal RefundedVolume { get; init; }
        public int Companies { get; init; }
        public int AuditEventsLast7Days { get; init; }
    }
}
