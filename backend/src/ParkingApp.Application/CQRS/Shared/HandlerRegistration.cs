using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS.Behaviors;
using ParkingApp.Domain.Events;

namespace ParkingApp.Application.CQRS;

/// <summary>
/// Result of convention-based CQRS / domain-event handler registration.
/// </summary>
public sealed record HandlerRegistrationResult(
    int CommandHandlers,
    int QueryHandlers,
    int DomainEventHandlers,
    int Behaviors,
    IReadOnlyList<string> MissingCommandHandlers,
    IReadOnlyList<string> MissingQueryHandlers)
{
    public int TotalHandlers => CommandHandlers + QueryHandlers + DomainEventHandlers;
    public bool IsComplete => MissingCommandHandlers.Count == 0 && MissingQueryHandlers.Count == 0;
}

/// <summary>
/// Assembly-scan registration for command/query/domain-event handlers and dispatcher behaviors.
/// </summary>
public static class HandlerRegistration
{
    /// <summary>
    /// Registers all closed handlers from <paramref name="assembly"/>.
    /// When <paramref name="throwIfMissingHandlers"/> is true, throws if any ICommand/IQuery has no handler.
    /// </summary>
    public static HandlerRegistrationResult AddHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        bool throwIfMissingHandlers = false)
    {
        var commandCount = 0;
        var queryCount = 0;
        var eventCount = 0;
        var behaviorCount = 0;

        var types = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsGenericTypeDefinition);

        foreach (var implementation in types)
        {
            foreach (var serviceType in implementation.GetInterfaces())
            {
                if (serviceType == typeof(IDispatcherBehavior))
                {
                    services.AddScoped(typeof(IDispatcherBehavior), implementation);
                    behaviorCount++;
                    continue;
                }

                if (!serviceType.IsGenericType)
                    continue;

                var definition = serviceType.GetGenericTypeDefinition();
                if (definition == typeof(ICommandHandler<,>))
                {
                    services.AddScoped(serviceType, implementation);
                    commandCount++;
                }
                else if (definition == typeof(IQueryHandler<,>))
                {
                    services.AddScoped(serviceType, implementation);
                    queryCount++;
                }
                else if (definition == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(serviceType, implementation);
                    eventCount++;
                }
            }
        }

        var missingCommands = FindMissingHandlers(
            assembly,
            openRequest: typeof(ICommand<>),
            openHandler: typeof(ICommandHandler<,>),
            services);

        var missingQueries = FindMissingHandlers(
            assembly,
            openRequest: typeof(IQuery<>),
            openHandler: typeof(IQueryHandler<,>),
            services);

        var result = new HandlerRegistrationResult(
            commandCount,
            queryCount,
            eventCount,
            behaviorCount,
            missingCommands,
            missingQueries);

        if (throwIfMissingHandlers && !result.IsComplete)
        {
            var parts = new List<string>();
            if (missingCommands.Count > 0)
                parts.Add("commands without handlers: " + string.Join(", ", missingCommands));
            if (missingQueries.Count > 0)
                parts.Add("queries without handlers: " + string.Join(", ", missingQueries));
            throw new InvalidOperationException(
                "CQRS registration incomplete — " + string.Join("; ", parts));
        }

        return result;
    }

    private static IReadOnlyList<string> FindMissingHandlers(
        Assembly assembly,
        Type openRequest,
        Type openHandler,
        IServiceCollection services)
    {
        var missing = new List<string>();

        var requestTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsGenericTypeDefinition)
            .Select(t =>
            {
                var requestIface = t.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == openRequest);
                return requestIface == null ? null : new { RequestType = t, ResultType = requestIface.GetGenericArguments()[0] };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        foreach (var item in requestTypes)
        {
            var handlerService = openHandler.MakeGenericType(item.RequestType, item.ResultType);
            var registered = services.Any(d => d.ServiceType == handlerService);
            if (!registered)
                missing.Add(item.RequestType.Name);
        }

        return missing;
    }
}
