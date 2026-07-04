using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Interfaces;

public interface ICompanyQuotaCache
{
    Task<IReadOnlyList<CompanyQuotaCacheEntry>> GetCompanyAllocationsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyQuotaCacheEntry?> GetAllocationAsync(Guid companyId, Guid allocationId, CancellationToken cancellationToken = default);
    Task InvalidateCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed record CompanyQuotaCacheEntry(
    Guid CompanyId,
    Guid AllocationId,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    decimal HourlyRate,
    bool ParkingSpaceIsActive,
    BillingType BillingType,
    AllocationStatus Status,
    ParkingAllocationSource SourceType,
    Guid? VendorId,
    string? LeaseReference,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    int TotalSlots,
    int FixedSlots,
    int SharedSlots,
    decimal MonthlyRate,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt,
    int MaxBookingsPerEmployeePerDay,
    int MaxBookingsPerEmployeePerWeek,
    int PriorityThreshold,
    TimeSpan AllowedStartTime,
    TimeSpan AllowedEndTime,
    bool AllowWeekends)
{
    public bool IsBookable => Status == AllocationStatus.Active && ParkingSpaceIsActive;
}
