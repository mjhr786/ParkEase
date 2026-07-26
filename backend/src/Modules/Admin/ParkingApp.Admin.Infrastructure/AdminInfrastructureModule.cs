using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Admin.Application.Interfaces;
using ParkingApp.Admin.Contracts;
using ParkingApp.Admin.Infrastructure.ReadStores;
using ParkingApp.Admin.Infrastructure.Services;

namespace ParkingApp.Admin.Infrastructure;

/// <summary>
/// Admin module infrastructure. Host must register <see cref="Persistence.IAdminDbContext"/>.
/// </summary>
public static class AdminInfrastructureModule
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAdminAudit, AdminAudit>();
        services.AddScoped<IAdminReadStore, AdminReadStore>();
        return services;
    }
}
