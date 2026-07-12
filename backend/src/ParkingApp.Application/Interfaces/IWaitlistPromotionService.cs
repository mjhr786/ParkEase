using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Promotes corporate waitlist entries to confirmed bookings (admin or system).
/// </summary>
public interface IWaitlistPromotionService
{
    /// <param name="adminUserId">When set, requires company admin. When null, system auto-promotion (no admin check).</param>
    Task<ApiResponse<CorporateReservationResultDto>> PromoteAsync(
        Guid companyId,
        Guid waitlistEntryId,
        Guid? adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires stale pending entries and attempts auto-promotion for eligible queue heads.
    /// </summary>
    Task<WaitlistAutoPromotionBatchResult> ProcessPendingAsync(
        int batchSize = 25,
        CancellationToken cancellationToken = default);
}

public sealed record WaitlistAutoPromotionBatchResult(
    int Promoted,
    int Expired,
    int Attempted,
    int Skipped);

public sealed record WaitlistPromotionCandidate(
    Guid CompanyId,
    Guid WaitlistEntryId,
    Guid AllocationId,
    DateTime RequestedStartDateTime,
    DateTime RequestedEndDateTime,
    int PriorityAtRequest,
    DateTime CreatedAt);

/// <summary>
/// Read/write port for waitlist auto-promotion scanning (Infrastructure).
/// </summary>
public interface IWaitlistPromotionStore
{
    Task<int> ExpireStalePendingAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WaitlistPromotionCandidate>> GetPromotionCandidatesAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default);
}
