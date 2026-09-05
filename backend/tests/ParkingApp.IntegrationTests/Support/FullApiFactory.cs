using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ParkingApp.API.Options;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Application.Options;
using ParkingApp.Infrastructure.Services;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Notifications.Contracts;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// L4 full-pipeline HTTP host: real JWT, real dispatcher, EF Migrate against Testcontainers PostGIS.
/// Channel isolation is configurable. Background hosted services are stripped for stable IT runs.
/// Payment gateway is replaced with <see cref="DeterministicPaymentService"/> (no Stripe network).
/// External auth uses <see cref="FakeExternalTokenValidator"/> (no real JWKS).
/// </summary>
public sealed class FullApiFactory : WebApplicationFactory<Program>
{
    public const string JwtSecret = "FullApiIntegrationTestSecretKey_AtLeast32Chars!";
    public const string JwtIssuer = "ParkingApp";
    public const string JwtAudience = "ParkingApp";

    private readonly string _connectionString;
    private readonly bool _channelIsolationEnabled;
    private readonly bool _externalAuthEnabled;

    public FullApiFactory(
        string connectionString,
        bool channelIsolationEnabled = false,
        bool externalAuthEnabled = true)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
        _channelIsolationEnabled = channelIsolationEnabled;
        _externalAuthEnabled = externalAuthEnabled;
    }

    public bool ChannelIsolationEnabled => _channelIsolationEnabled;

    /// <summary>Shared deterministic gateway instance (signature verify / order ids for HTTP IT).</summary>
    public DeterministicPaymentService PaymentService { get; } = new();

    /// <summary>Stub IdP validator registered for this host (CI-safe).</summary>
    public FakeExternalTokenValidator FakeExternalTokens { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Force Development: process/host may default to Production (breaks JWT HTTPS metadata + secrets).
        builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Development);
        builder.UseEnvironment(Environments.Development);
        // Host settings are visible early; ConfigureAppConfiguration alone can lose to appsettings placeholders.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Logging:File:Enabled", "false");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Highest-priority overrides for IT (after appsettings / user-secrets).
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                // Empty Redis → in-memory cache registration path in AddInfrastructure
                ["ConnectionStrings:Redis"] = "",
                ["Jwt:SecretKey"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["ChannelIsolation:Enabled"] = _channelIsolationEnabled ? "true" : "false",
                ["ChannelIsolation:EnforceCompanyClaimMatch"] = "true",
                ["ChannelIsolation:VendorAllocationAllowlistEnabled"] = "true",
                ["ChannelIsolation:TreatMissingClaimAs"] = "Marketplace",
                ["ExternalAuth:Enabled"] = _externalAuthEnabled ? "true" : "false",
                ["ExternalAuth:RateLimitPerMinute"] = "1000",
                ["ExternalAuth:Providers:Google:Enabled"] = _externalAuthEnabled ? "true" : "false",
                ["ExternalAuth:Providers:Google:ClientIds:0"] = "test-google-client",
                ["ExternalAuth:Providers:Apple:Enabled"] = _externalAuthEnabled ? "true" : "false",
                ["ExternalAuth:Providers:Apple:ClientIds:0"] = "test.apple.client",
                ["RateLimiting:Disabled"] = "true",
                ["RateLimiting:Enabled"] = "false",
                ["Logging:File:Enabled"] = "false",
                ["Logging:Serilog:MinimumLevel"] = "Warning",
                ["Storage:Provider"] = "Local",
                ["API_BASE_URL"] = "http://localhost",
                // Full pipeline owns migrate against Testcontainers
                ["Database:ApplyMigrationsOnStartup"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Avoid outbox / background DB work during HTTP IT
            services.RemoveAll<IHostedService>();

            // CI has no user secrets: Program may have bound the appsettings placeholder.
            // Re-bind EF + Dapper to the Testcontainers connection string.
            TestDbContextRegistration.ReplacePostgres(services, _connectionString);

            // Guarantee in-memory cache even if host already bound Redis from secrets
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();

            // No real Stripe in IT — deterministic create-order / verify / refund
            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService>(PaymentService);

            // Outbox processes domain events on SaveChanges — stub external delivery so HTTP IT
            // does not hang on Resend/Firebase/network.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, NoOpEmailService>();
            services.RemoveAll<IPushNotificationService>();
            services.AddSingleton<IPushNotificationService, NoOpPushNotificationService>();
            services.RemoveAll<INotificationCoordinator>();
            services.AddSingleton<INotificationCoordinator, NoOpNotificationCoordinator>();

            // Never call real Google/Apple JWKS in CI
            services.RemoveAll<IExternalTokenValidator>();
            services.AddSingleton<IExternalTokenValidator>(FakeExternalTokens);

            // Force JWT validation to the IT signing key (Program may have bound a different secret).
            // MapInboundClaims=true so JWT "sub" → ClaimTypes.NameIdentifier (AuthController.GetUserId).
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

            // Force isolation flag after Options bind (config order can be flaky under WAF)
            services.PostConfigure<ChannelIsolationOptions>(options =>
            {
                options.Enabled = _channelIsolationEnabled;
                options.EnforceCompanyClaimMatch = true;
                options.VendorAllocationAllowlistEnabled = true;
                options.TreatMissingClaimAs = "Marketplace";
            });

            services.PostConfigure<ExternalAuthOptions>(options =>
            {
                options.Enabled = _externalAuthEnabled;
                options.RateLimitPerMinute = 1000;
                options.Providers["Google"] = new ExternalProviderOptions
                {
                    Enabled = _externalAuthEnabled,
                    ClientIds = new List<string> { "test-google-client" }
                };
                options.Providers["Apple"] = new ExternalProviderOptions
                {
                    Enabled = _externalAuthEnabled,
                    ClientIds = new List<string> { "test.apple.client" }
                };
            });
        });
    }
}
