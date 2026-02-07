using Microsoft.Extensions.DependencyInjection;

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
/// Implementation of IDispatcher using DI container to resolve handlers
/// </summary>
public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        
        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for command type {commandType.Name}");
        }

        var method = handlerType.GetMethod("HandleAsync");
        if (method == null)
        {
            throw new InvalidOperationException($"HandleAsync method not found on handler for {commandType.Name}");
        }

        var result = method.Invoke(handler, new object[] { command, cancellationToken });
        return await (Task<TResult>)result!;
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        
        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for query type {queryType.Name}");
        }

        var method = handlerType.GetMethod("HandleAsync");
        if (method == null)
        {
            throw new InvalidOperationException($"HandleAsync method not found on handler for {queryType.Name}");
        }

        var result = method.Invoke(handler, new object[] { query, cancellationToken });
        return await (Task<TResult>)result!;
    }
}
