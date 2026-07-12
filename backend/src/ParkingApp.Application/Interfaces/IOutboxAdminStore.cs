using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Admin read/ops over the transactional outbox (Infrastructure implements with EF).
/// </summary>
public interface IOutboxAdminStore
{
    Task<OutboxMessageListResultDto> ListAsync(
        OutboxMessageStatusDto? status,
        string? typeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OutboxMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset a Failed/Pending message so it is eligible immediately
    /// (clears error, AvailableAfterUtc = now, Status = Pending).
    /// </summary>
    Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Requeue all Failed messages for immediate retry.</summary>
    Task<int> RequeueAllFailedAsync(CancellationToken cancellationToken = default);
}
