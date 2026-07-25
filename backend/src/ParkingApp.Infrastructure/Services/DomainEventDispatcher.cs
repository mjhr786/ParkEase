using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Domain;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// Resolves and invokes all registered BuildingBlocks <see cref="IDomainEventHandler{TEvent}"/> for each event.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchEventsAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

            foreach (var handler in _serviceProvider.GetServices(handlerType))
            {
                if (handler is null)
                    continue;

                try
                {
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method is null)
                        continue;

                    var task = (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                    await task;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling domain event {EventType}", eventType.Name);
                }
            }
        }
    }
}
