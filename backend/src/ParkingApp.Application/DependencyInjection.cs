using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS;

namespace ParkingApp.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Shared host CQRS registration.
    /// Call module application extensions from the composition root.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddCQRS(throwIfMissingHandlers: false);
        return services;
    }
}
