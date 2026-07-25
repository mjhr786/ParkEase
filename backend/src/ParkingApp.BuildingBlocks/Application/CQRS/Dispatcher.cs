using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS.Behaviors;

namespace ParkingApp.Application.CQRS;

/// <summary>
/// Dispatcher for sending commands and queries to their handlers
/// </summary>
public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IDispatcher using DI container to resolve handlers and pipeline behaviors.
/// </summary>
public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        return ExecutePipelineAsync<TResult>(
            command,
            isCommand: true,
            typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult)),
            cancellationToken);
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        return ExecutePipelineAsync<TResult>(
            query,
            isCommand: false,
            typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult)),
            cancellationToken);
    }

    private async Task<TResult> ExecutePipelineAsync<TResult>(
        object request,
        bool isCommand,
        Type handlerInterfaceType,
        CancellationToken cancellationToken)
    {
        var handler = _serviceProvider.GetService(handlerInterfaceType);
        if (handler == null)
        {
            var kind = isCommand ? "command" : "query";
            throw new InvalidOperationException(
                $"No handler registered for {kind} type {request.GetType().Name}");
        }

        var method = handlerInterfaceType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException(
                $"HandleAsync method not found on handler for {request.GetType().Name}");

        RequestHandlerDelegate<TResult> handlerDelegate = async () =>
        {
            var invokeResult = method.Invoke(handler, new[] { request, cancellationToken });
            return await (Task<TResult>)invokeResult!;
        };

        // Prefer GetService(IEnumerable<>) so missing registration is empty (not throw) — keeps unit tests simple
        var behaviors = (
                _serviceProvider.GetService(typeof(IEnumerable<IDispatcherBehavior>))
                    as IEnumerable<IDispatcherBehavior>
                ?? Enumerable.Empty<IDispatcherBehavior>())
            .OrderBy(b => b.Order)
            .ToList();

        // Build pipeline: outermost behavior first (lowest Order)
        RequestHandlerDelegate<TResult> pipeline = handlerDelegate;
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () => behavior.HandleAsync(request, isCommand, next, cancellationToken);
        }

        return await pipeline();
    }
}
