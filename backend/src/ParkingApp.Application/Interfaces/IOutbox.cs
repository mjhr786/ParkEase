using ParkingApp.Domain.Events;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Writes domain events into the transactional outbox (same DbContext as the UoW).
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Stages an outbox row for the given domain event (persisted on the next SaveChanges).
    /// </summary>
    void Enqueue(IDomainEvent domainEvent);
}

/// <summary>
/// Processes pending outbox messages (handlers run after the write transaction commits).
/// </summary>
public interface IOutboxProcessor
{
    /// <returns>Number of messages successfully processed in this batch.</returns>
    Task<int> ProcessPendingAsync(int batchSize = 50, CancellationToken cancellationToken = default);
}
