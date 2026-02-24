using System.Data;
using Dapper;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public DashboardRepository(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<VendorAggregateRow> GetVendorAggregatesAsync(Guid vendorId, DateTime startOfMonth, DateTime startOfWeek, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                (SELECT COUNT(*) FROM "ParkingSpaces" WHERE "OwnerId" = @VendorId AND "IsDeleted" = FALSE) AS "TotalParkingSpaces",
                (SELECT COUNT(*) FROM "ParkingSpaces" WHERE "OwnerId" = @VendorId AND "IsDeleted" = FALSE AND "IsActive" = TRUE) AS "ActiveParkingSpaces",
                COALESCE((SELECT AVG("Rating") FROM "Reviews" r INNER JOIN "ParkingSpaces" ps ON r."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId), 0) AS "AverageRating",
                (SELECT COUNT(*) FROM "Reviews" r INNER JOIN "ParkingSpaces" ps ON r."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId) AS "TotalReviews",
                (SELECT COUNT(*) FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId) AS "TotalBookings",
                (SELECT COUNT(*) FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 2) AS "ActiveBookings",
                (SELECT COUNT(*) FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 0) AS "PendingBookings",
                (SELECT COUNT(*) FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 3) AS "CompletedBookings",
                COALESCE((SELECT SUM("TotalAmount") FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 3), 0) AS "TotalEarnings",
                COALESCE((SELECT SUM("TotalAmount") FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 3 AND b."CreatedAt" >= @StartOfMonth), 0) AS "MonthlyEarnings",
                COALESCE((SELECT SUM("TotalAmount") FROM "Bookings" b INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id" WHERE ps."OwnerId" = @VendorId AND b."Status" = 3 AND b."CreatedAt" >= @StartOfWeek), 0) AS "WeeklyEarnings"
            """;
        
        var result = await connection.QueryFirstOrDefaultAsync<VendorAggregateRow>(sql, new { VendorId = vendorId, StartOfMonth = startOfMonth, StartOfWeek = startOfWeek });
        return result ?? new VendorAggregateRow();
    }

    public async Task<List<EarningsChartDataDto>> GetEarningsChartDataAsync(Guid vendorId, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT TO_CHAR(date_trunc('day', b."CreatedAt"), 'Dy') AS "Label", SUM("TotalAmount") AS "Amount"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            WHERE ps."OwnerId" = @VendorId AND b."Status" = 3 AND b."CreatedAt" >= CURRENT_DATE - INTERVAL '7 days'
            GROUP BY date_trunc('day', b."CreatedAt")
            ORDER BY date_trunc('day', b."CreatedAt")
            """;
        
        return (await connection.QueryAsync<EarningsChartDataDto>(sql, new { VendorId = vendorId }) ?? Enumerable.Empty<EarningsChartDataDto>()).ToList();
    }

    public async Task<List<BookingDto>> GetRecentVendorBookingsAsync(Guid vendorId, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", NULL AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            WHERE ps."OwnerId" = @VendorId AND b."IsDeleted" = FALSE
            ORDER BY b."CreatedAt" DESC
            LIMIT 5
            """;
        
        return (await connection.QueryAsync<BookingDto>(sql, new { VendorId = vendorId }) ?? Enumerable.Empty<BookingDto>()).ToList();
    }

    public async Task<MemberAggregateRow> GetMemberAggregatesAsync(Guid memberId, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                COUNT(*) AS "TotalBookings",
                COUNT(*) FILTER (WHERE "Status" = 2) AS "ActiveBookings",
                COUNT(*) FILTER (WHERE "Status" = 3) AS "CompletedBookings",
                COALESCE(SUM("TotalAmount") FILTER (WHERE "Status" = 3), 0) AS "TotalSpent"
            FROM "Bookings"
            WHERE "UserId" = @MemberId AND "IsDeleted" = FALSE
            """;
        
        var result = await connection.QueryFirstOrDefaultAsync<MemberAggregateRow>(sql, new { MemberId = memberId });
        return result ?? new MemberAggregateRow();
    }

    public async Task<List<BookingDto>> GetUpcomingMemberBookingsAsync(Guid memberId, DateTime now, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", p."Status" AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE b."UserId" = @MemberId AND b."IsDeleted" = FALSE AND b."StartDateTime" > @Now AND b."Status" IN (0, 1, 6, 2)
            ORDER BY b."StartDateTime" ASC
            LIMIT 5
            """;
        
        return (await connection.QueryAsync<BookingDto>(sql, new { MemberId = memberId, Now = now }) ?? Enumerable.Empty<BookingDto>()).ToList();
    }

    public async Task<List<BookingDto>> GetRecentMemberBookingsAsync(Guid memberId, CancellationToken ct = default)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT 
                b."Id", b."UserId", CONCAT(u."FirstName", ' ', u."LastName") AS "UserName",
                b."ParkingSpaceId", ps."Title" AS "ParkingSpaceTitle", ps."Address" AS "ParkingSpaceAddress",
                ps."Latitude", ps."Longitude",
                b."StartDateTime", b."EndDateTime", b."PricingType", b."VehicleType",
                b."VehicleNumber", b."VehicleModel",
                b."BaseAmount", b."TaxAmount", b."ServiceFee", b."DiscountAmount", b."TotalAmount",
                b."DiscountCode", b."Status", b."BookingReference",
                b."CheckInTime", b."CheckOutTime", p."Status" AS "PaymentStatus", b."CreatedAt"
            FROM "Bookings" b
            INNER JOIN "ParkingSpaces" ps ON b."ParkingSpaceId" = ps."Id"
            INNER JOIN "Users" u ON b."UserId" = u."Id"
            LEFT JOIN "Payments" p ON p."BookingId" = b."Id" AND p."IsDeleted" = FALSE
            WHERE b."UserId" = @MemberId AND b."IsDeleted" = FALSE AND b."Status" = 3
            ORDER BY b."CheckOutTime" DESC
            LIMIT 5
            """;
        
        return (await connection.QueryAsync<BookingDto>(sql, new { MemberId = memberId }) ?? Enumerable.Empty<BookingDto>()).ToList();
    }
}
