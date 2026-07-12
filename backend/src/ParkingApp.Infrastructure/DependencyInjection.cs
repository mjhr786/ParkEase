using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Caching;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.ReadModel.Bookings;
using ParkingApp.Infrastructure.ReadModel.Corporate;
using ParkingApp.Infrastructure.ReadModel.Parking;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Infrastructure.ReadModel.Reviews;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - PostgreSQL with PostGIS
        var connectionString = NormalizeNpgsqlPooling(
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is required"));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            }));
        services.AddMemoryCache();

        // Dapper SQL connection factory (same pool-friendly connection string)
        services.AddSingleton<ISqlConnectionFactory>(new NpgsqlConnectionFactory(connectionString));

        // Domain Events (still used if callers dispatch directly; primary path is outbox)
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Transactional outbox
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IOutboxAdminStore, OutboxAdminStore>();
        services.AddHostedService<OutboxBackgroundService>();

        // Corporate waitlist auto-promotion
        services.Configure<WaitlistAutoPromotionOptions>(
            configuration.GetSection(WaitlistAutoPromotionOptions.SectionName));
        services.AddScoped<IWaitlistPromotionStore, WaitlistPromotionStore>();
        services.AddHostedService<WaitlistAutoPromotionBackgroundService>();

        // Unit of Work: one implementation; context ports resolve to the same scoped instance
        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IMarketplaceUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IMessagingUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ICorporateUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IParkingSpaceRepository, ParkingSpaceRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ICompanyReadStore, CompanyReadStore>();
        services.AddScoped<IParkingReadStore, ParkingReadStore>();
        services.AddScoped<IBookingReadStore, BookingReadStore>();
        services.AddScoped<IReviewReadStore, ReviewReadStore>();
        services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();

        // Services
        services.AddScoped<ICorporateTenantContext, CorporateTenantContext>();
        services.AddScoped<ICompanyQuotaCache, CompanyQuotaCache>();
        services.AddSingleton<ICorporateWebLinkBuilder, CorporateWebLinkBuilder>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddScoped<IParkingAvailabilityModelService, ParkingAvailabilityMlModelService>();
        services.AddHttpClient<IEmailService, ResendEmailService>();
        services.AddHttpClient<IRoutingService, OSRMService>();

        RegisterCache(services, configuration);

        return services;
    }

    /// <summary>
    /// Prefer Upstash/Redis when ConnectionStrings:Redis is configured; otherwise in-memory.
    /// Connection is lazy (first resolve). Operations fail-open inside <see cref="RedisCacheService"/>.
    /// </summary>
    private static void RegisterCache(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisCacheOptions>(configuration.GetSection(RedisCacheOptions.SectionName));

        var redisConnection = configuration.GetConnectionString("Redis");

        if (!RedisConnectionFactory.IsConfigured(redisConnection))
        {
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            Console.WriteLine(">> Using IN-MEMORY Cache (Redis not configured)");
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ParkingApp.Redis");
            return RedisConnectionFactory.Connect(redisConnection!, options, logger);
        });

        services.AddSingleton<ICacheService>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
            var options = sp.GetRequiredService<IOptions<RedisCacheOptions>>();
            return new RedisCacheService(redis, logger, options);
        });

        var instanceName = configuration["Redis:InstanceName"] ?? "ParkEase_";
        Console.WriteLine($">> Using REDIS Cache (Upstash ParkEase, instance={instanceName})");
    }

    /// <summary>
    /// Tune Npgsql for direct Postgres vs Supabase/PgBouncer transaction poolers.
    /// Forcing client pooling against port 6543 often yields dead sockets and
    /// "Timeout during reading attempt" on background polls.
    /// </summary>
    internal static string NormalizeNpgsqlPooling(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        var isTransactionPooler =
            builder.Port == 6543
            || (builder.Host?.Contains("pooler", StringComparison.OrdinalIgnoreCase) ?? false);

        // Multiplexing is unsafe/unhelpful with transaction-mode poolers and background services.
        builder.Multiplexing = false;

        if (isTransactionPooler)
        {
            // PgBouncer transaction mode: prefer no client-side pool (or very short-lived connections).
            // Matches prior intentional Pooling=false on Supabase connection strings.
            builder.Pooling = false;
            if (builder.Timeout < 30)
                builder.Timeout = 30;
            if (builder.CommandTimeout < 30)
                builder.CommandTimeout = 30;
            // Keepalive helps detect half-open TCP through long idle periods (dev laptop sleep, etc.).
            if (builder.KeepAlive == 0)
                builder.KeepAlive = 30;
        }
        else
        {
            // Direct Postgres: client pooling is fine.
            builder.Pooling = true;
            if (builder.MaxPoolSize < 20)
                builder.MaxPoolSize = 50;
            if (builder.Timeout < 15)
                builder.Timeout = 15;
            if (builder.CommandTimeout < 30)
                builder.CommandTimeout = 30;
        }

        return builder.ConnectionString;
    }
}
