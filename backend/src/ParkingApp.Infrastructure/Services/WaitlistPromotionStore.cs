using Dapper;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Infrastructure.Services;

public sealed class WaitlistPromotionStore : IWaitlistPromotionStore
{
    private readonly ISqlConnectionFactory _sql;

    public WaitlistPromotionStore(ISqlConnectionFactory sql)
    {
        _sql = sql;
    }

    public async Task<int> ExpireStalePendingAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "CorporateWaitlistEntries"
            SET
                "Status" = @CancelledStatus,
                "CancelledAt" = @UtcNow,
                "UpdatedAt" = @UtcNow
            WHERE "IsDeleted" = FALSE
                AND "Status" = @PendingStatus
                AND "RequestedEndDateTime" <= @UtcNow;
            """;

        using var connection = _sql.CreateConnection();
        return await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    UtcNow = utcNow,
                    PendingStatus = (int)WaitlistStatus.Pending,
                    CancelledStatus = (int)WaitlistStatus.Cancelled
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<WaitlistPromotionCandidate>> GetPromotionCandidatesAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Prefer highest priority / earliest queue position; only windows still open.
        const string sql = """
            SELECT
                w."CompanyId" AS CompanyId,
                w."Id" AS WaitlistEntryId,
                w."AllocationId" AS AllocationId,
                w."RequestedStartDateTime" AS RequestedStartDateTime,
                w."RequestedEndDateTime" AS RequestedEndDateTime,
                w."PriorityAtRequest" AS PriorityAtRequest,
                w."CreatedAt" AS CreatedAt
            FROM "CorporateWaitlistEntries" w
            INNER JOIN "ParkingAllocations" pa
                ON pa."Id" = w."AllocationId"
                AND pa."IsDeleted" = FALSE
                AND pa."Status" = @ActiveAllocationStatus
            INNER JOIN "Companies" c
                ON c."Id" = w."CompanyId"
                AND c."IsDeleted" = FALSE
                AND c."IsActive" = TRUE
            WHERE w."IsDeleted" = FALSE
                AND w."Status" = @PendingStatus
                AND w."RequestedEndDateTime" > @UtcNow
            ORDER BY w."PriorityAtRequest" DESC, w."CreatedAt" ASC
            LIMIT @Take;
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<WaitlistPromotionCandidate>(
            new CommandDefinition(
                sql,
                new
                {
                    UtcNow = utcNow,
                    Take = take,
                    PendingStatus = (int)WaitlistStatus.Pending,
                    ActiveAllocationStatus = (int)AllocationStatus.Active
                },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
