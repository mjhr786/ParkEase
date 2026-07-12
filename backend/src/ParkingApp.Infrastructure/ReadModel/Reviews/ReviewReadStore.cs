using Dapper;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.ReadModel.Reviews;

public sealed class ReviewReadStore : IReviewReadStore
{
    private readonly ISqlConnectionFactory _sql;

    public ReviewReadStore(ISqlConnectionFactory sql)
    {
        _sql = sql;
    }

    public async Task<IReadOnlyList<ReviewDto>> GetByParkingSpaceAsync(Guid parkingSpaceId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                r."Id", r."UserId",
                CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                r."ParkingSpaceId", r."BookingId",
                r."Rating", r."Title", r."Comment", r."HelpfulCount",
                r."OwnerResponse", r."OwnerResponseAt", r."CreatedAt"
            FROM "Reviews" r
            INNER JOIN "Users" u ON r."UserId" = u."Id"
            WHERE r."ParkingSpaceId" = @ParkingSpaceId AND r."IsDeleted" = FALSE
            ORDER BY r."CreatedAt" DESC
            """;

        using var connection = _sql.CreateConnection();
        var rows = await connection.QueryAsync<ReviewDto>(
            new CommandDefinition(sql, new { ParkingSpaceId = parkingSpaceId }, cancellationToken: ct));
        return rows.ToList();
    }
}
