namespace ParkingApp.BuildingBlocks.Persistence;

/// <summary>
/// Persist + transaction boundary. Implemented by Infrastructure UnitOfWork.
/// </summary>
public interface IUnitOfWorkTransaction
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
