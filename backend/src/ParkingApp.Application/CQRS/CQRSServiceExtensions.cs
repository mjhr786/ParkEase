using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ParkingApp.Application.CQRS;

public static class CQRSServiceExtensions
{
    /// <summary>
    /// Registers the dispatcher and convention-scans the Application assembly for
    /// command/query handlers, domain event handlers, and pipeline behaviors.
    /// </summary>
    /// <param name="throwIfMissingHandlers">
    /// When true, fails fast if any <see cref="ICommand{TResult}"/> / <see cref="IQuery{TResult}"/>
    /// in the assembly lacks a handler (useful in Development / tests).
    /// </param>
    /// <param name="additionalAssemblies">
    /// Optional module Application assemblies (e.g. future Messaging.Application) to scan.
    /// Missing-handler checks still run against the host Application assembly.
    /// </param>
    public static IServiceCollection AddCQRS(
        this IServiceCollection services,
        bool throwIfMissingHandlers = false,
        params Assembly[] additionalAssemblies)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var hostAssembly = Assembly.GetExecutingAssembly();
        var result = services.AddHandlersFromAssembly(hostAssembly, throwIfMissingHandlers);

        if (additionalAssemblies is { Length: > 0 })
        {
            var commandTotal = result.CommandHandlers;
            var queryTotal = result.QueryHandlers;
            var eventTotal = result.DomainEventHandlers;
            var behaviorTotal = result.Behaviors;

            foreach (var assembly in additionalAssemblies.Where(a => a is not null && a != hostAssembly).Distinct())
            {
                // Do not throw for missing handlers on module assemblies; host owns the completeness check.
                var moduleResult = services.AddHandlersFromAssembly(assembly, throwIfMissingHandlers: false);
                commandTotal += moduleResult.CommandHandlers;
                queryTotal += moduleResult.QueryHandlers;
                eventTotal += moduleResult.DomainEventHandlers;
                behaviorTotal += moduleResult.Behaviors;
            }

            result = new HandlerRegistrationResult(
                commandTotal,
                queryTotal,
                eventTotal,
                behaviorTotal,
                result.MissingCommandHandlers,
                result.MissingQueryHandlers);
        }

        // Keep a breadcrumb for diagnostics / tests without static mutable state
        services.AddSingleton(result);

        return services;
    }
}
