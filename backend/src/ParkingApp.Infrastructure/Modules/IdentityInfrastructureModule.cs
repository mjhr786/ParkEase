using Microsoft.Extensions.DependencyInjection;

using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Identity.Infrastructure.Persistence;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;
using ParkingApp.Identity.Infrastructure.Services;
using ParkingApp.Identity.Application.Interfaces;

namespace ParkingApp.Infrastructure.Modules;

/// <summary>
/// Host bridge for Identity: shared DbContext/UoW facades + auth services + module infrastructure.
/// </summary>
public static class IdentityInfrastructureModule
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        ParkingApp.Identity.Infrastructure.IdentityInfrastructureModule.AddIdentityInfrastructure(services);
        return services;
    }
}
