using Npgsql;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// Minimal Postgres DDL matching production column names used by
/// class-scoped occupancy SQL and dual-pool read-store queries.
/// </summary>
internal static class DualPoolSqlSchema
{
    public static async Task EnsureOccupancyTablesAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS "CorporateBookings" CASCADE;
            DROP TABLE IF EXISTS "Bookings" CASCADE;

            CREATE TABLE "Bookings" (
                "Id" uuid PRIMARY KEY,
                "StartDateTime" timestamptz NOT NULL,
                "EndDateTime" timestamptz NOT NULL,
                "Status" integer NOT NULL DEFAULT 1,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "SlotNumber" integer NULL,
                "VehicleType" integer NULL,
                "VehicleNumber" text NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT NOW()
            );

            CREATE TABLE "CorporateBookings" (
                "Id" uuid PRIMARY KEY,
                "CompanyId" uuid NOT NULL,
                "AllocationId" uuid NOT NULL,
                "MembershipId" uuid NOT NULL,
                "BookingId" uuid NOT NULL REFERENCES "Bookings"("Id"),
                "SlotType" integer NOT NULL DEFAULT 1,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamptz NOT NULL DEFAULT NOW()
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task EnsureReadStoreTablesAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS "ParkingAllocations" CASCADE;
            DROP TABLE IF EXISTS "ParkingSpaces" CASCADE;
            DROP TABLE IF EXISTS "Companies" CASCADE;
            DROP TABLE IF EXISTS "Users" CASCADE;

            CREATE TABLE "Users" (
                "Id" uuid PRIMARY KEY,
                "FirstName" text NULL,
                "LastName" text NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE
            );

            CREATE TABLE "Companies" (
                "Id" uuid PRIMARY KEY,
                "Name" text NOT NULL,
                "BillingType" integer NOT NULL DEFAULT 0,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE
            );

            CREATE TABLE "ParkingSpaces" (
                "Id" uuid PRIMARY KEY,
                "Title" text NOT NULL,
                "HourlyRate" numeric NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "OwnerId" uuid NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE
            );

            CREATE TABLE "ParkingAllocations" (
                "Id" uuid PRIMARY KEY,
                "CompanyId" uuid NOT NULL REFERENCES "Companies"("Id"),
                "ParkingSpaceId" uuid NOT NULL REFERENCES "ParkingSpaces"("Id"),
                "TotalSlots" integer NOT NULL DEFAULT 0,
                "FixedSlots" integer NOT NULL DEFAULT 0,
                "SharedSlots" integer NOT NULL DEFAULT 0,
                "TwoWheelerTotalSlots" integer NOT NULL DEFAULT 0,
                "TwoWheelerFixedSlots" integer NOT NULL DEFAULT 0,
                "TwoWheelerSharedSlots" integer NOT NULL DEFAULT 0,
                "FourWheelerTotalSlots" integer NOT NULL DEFAULT 0,
                "FourWheelerFixedSlots" integer NOT NULL DEFAULT 0,
                "FourWheelerSharedSlots" integer NOT NULL DEFAULT 0,
                "MonthlyRate" numeric NOT NULL DEFAULT 0,
                "StartDate" timestamptz NOT NULL,
                "EndDate" timestamptz NOT NULL,
                "Status" integer NOT NULL DEFAULT 1,
                "SourceType" integer NOT NULL DEFAULT 0,
                "VendorId" uuid NULL,
                "LeaseReference" text NULL,
                "ApprovedByUserId" uuid NULL,
                "ApprovedAt" timestamptz NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
                "MaxBookingsPerDay" integer NOT NULL DEFAULT 1,
                "MaxBookingsPerWeek" integer NOT NULL DEFAULT 5,
                "PriorityThreshold" integer NOT NULL DEFAULT 1,
                "AllowedStartTime" interval NOT NULL DEFAULT '07:00:00',
                "AllowedEndTime" interval NOT NULL DEFAULT '22:00:00',
                "AllowWeekends" boolean NOT NULL DEFAULT FALSE,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
