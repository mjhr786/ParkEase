using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;

namespace ParkingApp.Admin.Application;

/// <summary>
/// Admin module application registration. Call after <c>AddApplication</c>.
/// </summary>
public static class AdminApplicationModule
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly(), throwIfMissingHandlers: false);
        return services;
    }
}
