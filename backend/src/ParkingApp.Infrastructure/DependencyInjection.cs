using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Interfaces;

using ParkingApp.Corporate.Infrastructure;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Corporate.Domain.Interfaces; // ICorporateUnitOfWork (Corporate.Domain historical namespace)
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Infrastructure.Caching;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.ModuleAdapters;
using ParkingApp.Infrastructure.Modules;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;
using ParkingApp.Admin.Infrastructure;
using ParkingApp.Admin.Infrastructure.Persistence;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Shared infrastructure + module infrastructure registrations.
    /// Modules own repositories/read models/payment/routing; host owns DbContext, outbox, cache, email.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterSharedInfrastructure(services, configuration);

        services.AddIdentityInfrastructure();
        services.AddMarketplaceInfrastructure(configuration);
        services.AddCorporateInfrastructure(configuration);
        services.AddMessagingInfrastructure();
        services.AddAdminInfrastructure();

        // Notification delivery contract adapter (implementation registered by AddNotificationsModule)
        services.AddScoped<INotificationSender, NotificationSender>();

        return services;
    }

    private static void RegisterSharedInfrastructure(IServiceCollection services, IConfiguration configuration)
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

        // Transactional outbox (adaptive poll cadence for free-tier DB limits)
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<IOutboxAdminStore, OutboxAdminStore>();
        services.AddHostedService<OutboxBackgroundService>();

        // Unit of Work: one implementation; context ports resolve to the same scoped instance
        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        // BuildingBlocks transaction port for host Application TransactionBehavior (no host Domain dependency)
        services.AddScoped<ParkingApp.BuildingBlocks.Persistence.IUnitOfWorkTransaction>(
            sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ICorporateDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ICorporateUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Email (IEmailService / Resend) registers in Notifications.Infrastructure.AddNotificationServices

        RegisterCache(services, configuration);
    }

    /// <summary>
    /// Prefer Redis when ConnectionStrings:Redis is configured:
    /// Development ΓåÆ local Docker Redis; Production ΓåÆ Upstash (rediss://).
    /// Otherwise in-memory. Connection is lazy (first resolve). Operations fail-open inside <see cref="RedisCacheService"/>.
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
        var target = RedisConnectionFactory.DescribeTarget(redisConnection);
        Console.WriteLine($">> Using REDIS Cache ({target}, instance={instanceName})");
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
