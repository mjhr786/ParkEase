using Dapper;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Infrastructure.Services;

public sealed class CompanyQuotaCache : ICompanyQuotaCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ICacheService _cache;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public CompanyQuotaCache(ICacheService cache, ISqlConnectionFactory sqlConnectionFactory)
    {
        _cache = cache;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public Task<IReadOnlyList<CompanyQuotaCacheEntry>> GetCompanyAllocationsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCompanyKey(companyId);

        return _cache.GetOrSetAsync<IReadOnlyList<CompanyQuotaCacheEntry>>(
            cacheKey,
            async () =>
            {
                const string sql = """
                    SELECT
                        pa."CompanyId" AS CompanyId,
                        pa."Id" AS AllocationId,
                        pa."ParkingSpaceId" AS ParkingSpaceId,
                        ps."Title" AS ParkingSpaceTitle,
                        ps."HourlyRate" AS HourlyRate,
                        ps."IsActive" AS ParkingSpaceIsActive,
                        c."BillingType" AS BillingType,
                        pa."Status" AS Status,
                        pa."SourceType" AS SourceType,
                        pa."VendorId" AS VendorId,
                        pa."LeaseReference" AS LeaseReference,
                        pa."ApprovedByUserId" AS ApprovedByUserId,
                        pa."ApprovedAt" AS ApprovedAt,
                        pa."TotalSlots" AS TotalSlots,
                        pa."FixedSlots" AS FixedSlots,
                        pa."SharedSlots" AS SharedSlots,
                        pa."MonthlyRate" AS MonthlyRate,
                        pa."StartDate" AS StartDate,
                        pa."EndDate" AS EndDate,
                        pa."CreatedAt" AS CreatedAt,
                        pa."MaxBookingsPerDay" AS MaxBookingsPerEmployeePerDay,
                        pa."MaxBookingsPerWeek" AS MaxBookingsPerEmployeePerWeek,
                        pa."PriorityThreshold" AS PriorityThreshold,
                        pa."AllowedStartTime" AS AllowedStartTime,
                        pa."AllowedEndTime" AS AllowedEndTime,
                        pa."AllowWeekends" AS AllowWeekends
                    FROM "ParkingAllocations" pa
                    INNER JOIN "Companies" c ON c."Id" = pa."CompanyId"
                    INNER JOIN "ParkingSpaces" ps ON ps."Id" = pa."ParkingSpaceId"
                    WHERE pa."CompanyId" = @CompanyId
                        AND pa."IsDeleted" = FALSE
                        AND c."IsDeleted" = FALSE
                        AND ps."IsDeleted" = FALSE
                    ORDER BY pa."CreatedAt" DESC;
                    """;

                using var connection = _sqlConnectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<CompanyQuotaCacheRow>(
                    new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: cancellationToken));

                return rows.Select(row => row.ToCacheEntry()).ToList();
            },
            CacheDuration,
            cancellationToken);
    }

    public async Task<CompanyQuotaCacheEntry?> GetAllocationAsync(Guid companyId, Guid allocationId, CancellationToken cancellationToken = default)
    {
        var allocations = await GetCompanyAllocationsAsync(companyId, cancellationToken);
        return allocations.FirstOrDefault(a => a.AllocationId == allocationId);
    }

    public async Task InvalidateCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(BuildCompanyKey(companyId), cancellationToken);
    }

    private static string BuildCompanyKey(Guid companyId) => $"company-quota:{companyId}";

    private sealed class CompanyQuotaCacheRow
    {
        public Guid CompanyId { get; set; }
        public Guid AllocationId { get; set; }
        public Guid ParkingSpaceId { get; set; }
        public string ParkingSpaceTitle { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public bool ParkingSpaceIsActive { get; set; }
        public BillingType BillingType { get; set; }
        public AllocationStatus Status { get; set; }
        public ParkingAllocationSource SourceType { get; set; }
        public Guid? VendorId { get; set; }
        public string? LeaseReference { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int TotalSlots { get; set; }
        public int FixedSlots { get; set; }
        public int SharedSlots { get; set; }
        public decimal MonthlyRate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MaxBookingsPerEmployeePerDay { get; set; }
        public int MaxBookingsPerEmployeePerWeek { get; set; }
        public int PriorityThreshold { get; set; }
        public TimeSpan AllowedStartTime { get; set; }
        public TimeSpan AllowedEndTime { get; set; }
        public bool AllowWeekends { get; set; }

        public CompanyQuotaCacheEntry ToCacheEntry() => new(
            CompanyId,
            AllocationId,
            ParkingSpaceId,
            ParkingSpaceTitle,
            HourlyRate,
            ParkingSpaceIsActive,
            BillingType,
            Status,
            SourceType,
            VendorId,
            LeaseReference,
            ApprovedByUserId,
            ApprovedAt,
            TotalSlots,
            FixedSlots,
            SharedSlots,
            MonthlyRate,
            StartDate,
            EndDate,
            CreatedAt,
            MaxBookingsPerEmployeePerDay,
            MaxBookingsPerEmployeePerWeek,
            PriorityThreshold,
            AllowedStartTime,
            AllowedEndTime,
            AllowWeekends);
    }
}
