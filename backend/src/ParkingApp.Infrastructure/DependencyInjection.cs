using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;

namespace ParkingApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=parking.db";
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IParkingSpaceRepository, ParkingSpaceRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();

        // Services
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPaymentService, MockPaymentService>();
        services.AddScoped<Application.Interfaces.INotificationService, NotificationService>();

        return services;
    }
}
