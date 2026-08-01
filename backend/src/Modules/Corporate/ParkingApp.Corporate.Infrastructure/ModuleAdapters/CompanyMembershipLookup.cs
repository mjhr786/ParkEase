using Dapper;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Contracts;

namespace ParkingApp.Corporate.Infrastructure.ModuleAdapters;

/// <summary>
/// Dapper-backed membership lookup for Identity channel bind (no Domain leak to callers).
/// CompanyRole enum: Employee = 0, Admin = 1.
/// </summary>
internal sealed class CompanyMembershipLookup : ICompanyMembershipLookup
{
    private readonly ISqlConnectionFactory _sql;

    public CompanyMembershipLookup(ISqlConnectionFactory sql) => _sql = sql;

    public async Task<IReadOnlyList<CompanyMembershipSummary>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c."Id" AS CompanyId,
                c."Name" AS CompanyName,
                CASE WHEN m."Role" = 1 THEN 'Admin' ELSE 'Employee' END AS Role
            FROM "UserCompanyMemberships" m
            INNER JOIN "Companies" c ON c."Id" = m."CompanyId"
            WHERE m."UserId" = @UserId
              AND m."IsDeleted" = FALSE
              AND m."IsActive" = TRUE
              AND c."IsDeleted" = FALSE
              AND c."IsActive" = TRUE
            ORDER BY c."Name"
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<CompanyMembershipSummary>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<CompanyMembershipSummary?> GetActiveMembershipAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c."Id" AS CompanyId,
                c."Name" AS CompanyName,
                CASE WHEN m."Role" = 1 THEN 'Admin' ELSE 'Employee' END AS Role
            FROM "UserCompanyMemberships" m
            INNER JOIN "Companies" c ON c."Id" = m."CompanyId"
            WHERE m."UserId" = @UserId
              AND m."CompanyId" = @CompanyId
              AND m."IsDeleted" = FALSE
              AND m."IsActive" = TRUE
              AND c."IsDeleted" = FALSE
              AND c."IsActive" = TRUE
            LIMIT 1
            """;

        using var connection = _sql.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CompanyMembershipSummary>(
            new CommandDefinition(sql, new { UserId = userId, CompanyId = companyId }, cancellationToken: cancellationToken));
    }
}
