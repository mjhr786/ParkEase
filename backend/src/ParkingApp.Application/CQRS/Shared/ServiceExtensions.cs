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
    public static IServiceCollection AddCQRS(
        this IServiceCollection services,
        bool throwIfMissingHandlers = false)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var assembly = Assembly.GetExecutingAssembly();
        var result = services.AddHandlersFromAssembly(assembly, throwIfMissingHandlers);

        // Keep a breadcrumb for diagnostics / tests without static mutable state
        services.AddSingleton(result);

        return services;
    }
}
