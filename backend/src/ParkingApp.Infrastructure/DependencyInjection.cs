using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - PostgreSQL with PostGIS
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string is required");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.UseNetTopologySuite()));

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IParkingSpaceRepository, ParkingSpaceRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();

        // Services
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPaymentService, StripePaymentService>();
        
        // Cache Registration (Redis or In-Memory)
        var redisConnection = configuration.GetConnectionString("Redis");
        
        if (!string.IsNullOrWhiteSpace(redisConnection) && redisConnection != "localhost:6379")
        {
            // Use Redis if connection string is configured and not default
            var redisInstanceName = configuration["Redis:InstanceName"] ?? "ParkingApp_";
            
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var connectionString = ConfigurationOptions.Parse(redisConnection);
                connectionString.AbortOnConnectFail = false; 
                return ConnectionMultiplexer.Connect(connectionString);
            });
            
            services.AddSingleton<ICacheService>(sp =>
            {
                var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisCacheService>>();
                return new RedisCacheService(redis, logger, redisInstanceName);
            });
            
            Console.WriteLine(">> Using REDIS Cache");
        }
        else
        {
            // Fallback to In-Memory Cache
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            Console.WriteLine(">> Using IN-MEMORY Cache (Redis not configured)");
        }

        return services;
    }
}
