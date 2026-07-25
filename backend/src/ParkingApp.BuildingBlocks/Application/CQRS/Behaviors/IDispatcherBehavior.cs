namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Continuation of the dispatcher pipeline (next behavior or the handler).
/// </summary>
public delegate Task<TResult> RequestHandlerDelegate<TResult>();

/// <summary>
/// Cross-cutting pipeline step around command/query handlers.
/// Lower <see cref="Order"/> runs outermost (e.g. logging before validation).
/// </summary>
public interface IDispatcherBehavior
{
    /// <summary>Execution order; lower values wrap outer layers.</summary>
    int Order { get; }

    Task<TResult> HandleAsync<TResult>(
        object request,
        bool isCommand,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Marker for commands that should run inside a unit-of-work database transaction.
/// </summary>
public interface ITransactionalCommand
{
}
