using ParkingApp.BuildingBlocks.Persistence;

namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Optional UoW transaction around commands implementing <see cref="ITransactionalCommand"/>.
/// Does not wrap all commands by default (handlers already call SaveChanges).
/// Uses BuildingBlocks transaction port so host Application stays free of host Domain.
/// </summary>
public sealed class TransactionBehavior : IDispatcherBehavior
{
    private readonly IUnitOfWorkTransaction _unitOfWork;

    public TransactionBehavior(IUnitOfWorkTransaction unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public int Order => 20;

    public async Task<TResult> HandleAsync<TResult>(
        object request,
        bool isCommand,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        if (!isCommand || request is not ITransactionalCommand)
            return await next();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await next();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
