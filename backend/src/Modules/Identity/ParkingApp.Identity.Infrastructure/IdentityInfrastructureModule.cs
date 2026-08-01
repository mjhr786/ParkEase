using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Identity.Contracts;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Identity.Infrastructure.ModuleAdapters;
using ParkingApp.Identity.Infrastructure.Repositories;

namespace ParkingApp.Identity.Infrastructure;

/// <summary>
/// Identity module infrastructure registration (repos + outward contracts).
/// Host must register <c>IIdentityDbContext</c> and <c>IIdentityUnitOfWork</c> facades.
/// Host also registers <see cref="ISessionRebindService"/> (needs shared UoW + ITokenService).
/// </summary>
public static class IdentityInfrastructureModule
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<IDeviceTokenLookup, DeviceTokenLookup>();
        return services;
    }
}
