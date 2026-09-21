using System.IO.Compression;
using System.Threading.RateLimiting;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ParkingApp.API.Middleware;
using ParkingApp.API.Options;
using ParkingApp.Application;
using ParkingApp.Application.CQRS.Behaviors;
using ParkingApp.Application.Interfaces;
using ParkingApp.Infrastructure;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Notifications.Infrastructure;
using ParkingApp.Corporate.Application;
using ParkingApp.Identity.Application;
using ParkingApp.Marketplace.Application;
using ParkingApp.Messaging.Application;
using ParkingApp.Admin.Application;
using Serilog;
using Serilog.Events;
using ParkingApp.Marketplace.Infrastructure;

// Bootstrap logger until host configuration is available
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ParkEase API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog from configuration (file sink optional; shorter retention on free tier)
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        var config = context.Configuration;
        var isDev = context.HostingEnvironment.IsDevelopment();

        var minLevelName = config["Logging:Serilog:MinimumLevel"]
            ?? (isDev ? "Debug" : "Information");
        if (!Enum.TryParse<LogEventLevel>(minLevelName, ignoreCase: true, out var minLevel))
            minLevel = isDev ? LogEventLevel.Debug : LogEventLevel.Information;

        loggerConfiguration
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Default: file on in Development, off in Production (free-tier disk). Override via Logging:File:Enabled.
        var fileEnabled = config.GetValue<bool?>("Logging:File:Enabled") ?? isDev;
        if (fileEnabled)
        {
            var retained = config.GetValue("Logging:File:RetainedFileCountLimit", 5);
            retained = Math.Clamp(retained, 1, 30);
            loggerConfiguration.WriteTo.File(
                path: "logs/parkease-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retained,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
    });

    builder.Services.Configure<PerformanceLoggingOptions>(
        builder.Configuration.GetSection(PerformanceLoggingOptions.SectionName));
    builder.Services.Configure<MediaOptions>(
        builder.Configuration.GetSection(MediaOptions.SectionName));
    builder.Services.Configure<IotLprOptions>(
        builder.Configuration.GetSection(IotLprOptions.SectionName));
    builder.Services.Configure<ChannelIsolationOptions>(
        builder.Configuration.GetSection(ChannelIsolationOptions.SectionName));
    builder.Services.Configure<ParkingApp.Identity.Application.Options.ExternalAuthOptions>(
        builder.Configuration.GetSection(ParkingApp.Identity.Application.Options.ExternalAuthOptions.SectionName));
    builder.Services.Configure<ParkingApp.Identity.Application.Options.CorporateSsoOptions>(
        builder.Configuration.GetSection(ParkingApp.Identity.Application.Options.CorporateSsoOptions.SectionName));

    // Data Protection key ring for Corporate SSO secrets (KD-CS-12).
    // Dev: local FS. Staging/prod: set CorporateSso:DataProtectionKeysPath to a shared volume or configure Blob/Key Vault.
    var ssoKeysPath = builder.Configuration["CorporateSso:DataProtectionKeysPath"];
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("ParkEase");
    if (!string.IsNullOrWhiteSpace(ssoKeysPath))
    {
        Directory.CreateDirectory(ssoKeysPath);
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(ssoKeysPath));
        Log.Information("Data Protection keys path: {KeysPath}", ssoKeysPath);
    }
    else if (builder.Environment.IsDevelopment())
    {
        var devKeys = Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
        Directory.CreateDirectory(devKeys);
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(devKeys));
        Log.Information("Data Protection keys (dev): {KeysPath}", devKeys);
    }
    else
    {
        Log.Warning(
            "CorporateSso:DataProtectionKeysPath is not set. Multi-node SSO secret unprotect will fail unless a shared key ring is configured.");
    }

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddNotificationServices(builder.Configuration);

    builder.Services.AddApplication();
    builder.Services.AddCorporateApplication();
    builder.Services.AddIdentityApplication();
    builder.Services.AddMarketplaceApplication(builder.Configuration);
    builder.Services.AddMessagingApplication();
    builder.Services.AddAdminApplication();

    // Add Controllers with JSON source generator for hot DTO types
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, ParkingApp.API.Serialization.ParkEaseJsonContext.Default);
        });

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, ParkingApp.API.Serialization.ParkEaseJsonContext.Default);
    });

    // Response compression (Brotli + Gzip) for JSON/API and text-like payloads over HTTPS
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        // Defaults already include application/json; add common API/text types explicitly
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "application/json",
            "application/problem+json",
            "application/javascript",
            "text/css",
            "text/csv",
            "image/svg+xml"
        });
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest; // balance CPU vs size on API responses
    });
    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

    // Add CORS (origins overridable via Cors:AllowedOrigins in appsettings / env)
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[]
        {
            "http://localhost:5173",
            "https://localhost:5173",
            "http://localhost:3000",
            "http://127.0.0.1:5173",
            "http://localhost:5174",
            "https://localhost:5174",
            "https://parkease.azurewebsites.net",
            "http://parkeaseapp.runasp.net",
            "https://parkeaseapp.runasp.net",
            "http://masjidfinder.runasp.net",
            "https://masjidfinder.runasp.net",
            "https://parkease-aks.pages.dev",
        };

    builder.Services.AddCors(options =>
    {
        // SetPreflightMaxAge caches CORS preflight (OPTIONS) responses in the browser for 24 h.
        // After the first request from a given user agent, subsequent preflight checks are
        // skipped entirely — reducing total server load by 30–50% on authenticated mutation
        // endpoints (POST / PUT / DELETE with Authorization header).
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                  {
                      if (string.IsNullOrWhiteSpace(origin)) return false;

                      // 1. Explicitly configured origins
                      if (corsOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                          return true;

                      // 2. Cloudflare Pages production & preview subdomains (*.pages.dev)
                      try
                      {
                          var host = new Uri(origin).Host;
                          if (host.Equals("parkease-aks.pages.dev", StringComparison.OrdinalIgnoreCase) ||
                              host.EndsWith(".parkease-aks.pages.dev", StringComparison.OrdinalIgnoreCase) ||
                              host.EndsWith(".pages.dev", StringComparison.OrdinalIgnoreCase))
                          {
                              return true;
                          }
                      }
                      catch
                      {
                          // Invalid URI format
                      }

                      return false;
                  })
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromHours(24));
        });
    });

    // File storage (Marketplace module) - R2 when Storage:Provider=R2, else local wwwroot/uploads
    {
        var webRoot = builder.Environment.WebRootPath
            ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
        var publicBase = builder.Configuration["API_BASE_URL"] ?? "https://localhost:5173";
        builder.Services.AddMarketplaceFileStorage(builder.Configuration, webRoot, publicBase);
        Log.Information(
            FileStorageRegistration.IsR2Enabled(builder.Configuration)
                ? ">> Using Cloudflare R2 Storage"
                : ">> Using Local File Storage");
    }

    // SignalR: config-driven keepalive/timeout (defaults preserve healthy reconnects).
    // ClientTimeout must be > KeepAlive (typically ~2x). Free-tier friendly defaults: 30s / 60s.
    {
        var keepAliveSec = builder.Configuration.GetValue("SignalR:KeepAliveSeconds", 30);
        var clientTimeoutSec = builder.Configuration.GetValue("SignalR:ClientTimeoutSeconds", 60);
        keepAliveSec = Math.Clamp(keepAliveSec, 10, 120);
        clientTimeoutSec = Math.Clamp(clientTimeoutSec, keepAliveSec * 2, 300);

        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.KeepAliveInterval = TimeSpan.FromSeconds(keepAliveSec);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(clientTimeoutSec);
        });
    }

    // Configure JWT Authentication
    var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecretKey))
    {
        if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException("JWT:SecretKey must be configured in production environment");
        }
        // Use fallback only in development
        jwtSecretKey = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    }

    var key = Encoding.UTF8.GetBytes(jwtSecretKey);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ParkingApp",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ParkingApp",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Configure SignalR to use JWT from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    // Channel policies are optional documentation / defense-in-depth only (KD-5).
    // ChannelAuthorizationMiddleware is the authoritative allowlist enforcer.
    // Do not put [Authorize(Policy = "Channel*")] on controllers until soft-mode is removed (PR10b),
    // or soft Marketplace→corporate API access with flag off will break via UseAuthorization.
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ChannelMarketplace", policy =>
            policy.RequireClaim(
                ParkingApp.BuildingBlocks.Security.ParkEaseClaimTypes.Channel,
                "Marketplace",
                "Admin"));
        options.AddPolicy("ChannelCorporate", policy =>
            policy.RequireClaim(
                ParkingApp.BuildingBlocks.Security.ParkEaseClaimTypes.Channel,
                "Corporate",
                "Admin"));
        // Admin APIs keep [Authorize(Roles = "Admin")] — do not require Admin channel (KD-13).
    });

    // ─── Built-in Rate Limiting (replaces in-process RateLimitingMiddleware) ────────
    // Uses .NET 9's lock-free, GC-efficient sliding-window limiter partitioned by client IP.
    // Benefits over the old ConcurrentDictionary approach:
    //   • No static state — no unbounded RAM growth under traffic spikes.
    //   • Lock-free window segments — eliminates per-IP lock contention.
    //   • Native ASP.NET Core middleware — first-class cancellation, async safety.
    //
    // Route budgets (identical to old middleware; configurable via appsettings):
    //   /api/iot/*                  → Iot:Lpr:RateLimitPerMinute          (default 30)
    //   /api/auth/external/*        → ExternalAuth:RateLimitPerMinute     (default 20)
    //   /api/auth/corporate/sso/*   → CorporateSso:RateLimitPerMinute     (default 15)
    //   /api/*  (everything else)   → RateLimiting:MaxRequestsPerMinute   (default 100)
    //   /health, /hubs, /uploads, OPTIONS preflights → bypassed entirely
    var rateLimitingDisabled = builder.Configuration.GetValue("RateLimiting:Disabled", false);
    if (!rateLimitingDisabled)
    {
        var generalLimit = Math.Clamp(builder.Configuration.GetValue("RateLimiting:MaxRequestsPerMinute", 100), 1, 10_000);
        var iotLimit     = Math.Clamp(builder.Configuration.GetValue("Iot:Lpr:RateLimitPerMinute", 30), 1, 10_000);
        var authLimit    = Math.Clamp(builder.Configuration.GetValue("ExternalAuth:RateLimitPerMinute", 20), 1, 10_000);
        var ssoLimit     = Math.Clamp(builder.Configuration.GetValue("CorporateSso:RateLimitPerMinute", 15), 1, 10_000);

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var path = ctx.Request.Path;

                // OPTIONS preflights, health, hubs, and uploads bypass rate limiting.
                if (HttpMethods.IsOptions(ctx.Request.Method)
                    || path.StartsWithSegments("/health",  StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/hubs",    StringComparison.OrdinalIgnoreCase)
                    || path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetNoLimiter("bypass");
                }

                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // IoT LPR: strict per-device budget (device firmware sends frequent events)
                if (path.StartsWithSegments("/api/iot", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetSlidingWindowLimiter($"iot:{ip}", _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit          = iotLimit,
                            Window               = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow    = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit           = 0,
                        });

                // External auth (Google/Apple token exchange): tight budget to limit credential-stuffing
                if (path.StartsWithSegments("/api/auth/external", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetSlidingWindowLimiter($"auth-ext:{ip}", _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit          = authLimit,
                            Window               = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow    = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit           = 0,
                        });

                // Corporate SSO: tightest budget (token exchange & redirect-loop prevention)
                if (path.StartsWithSegments("/api/auth/corporate/sso", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetSlidingWindowLimiter($"sso:{ip}", _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit          = ssoLimit,
                            Window               = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow    = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit           = 0,
                        });

                // General API budget for all remaining /api/* routes
                if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetSlidingWindowLimiter($"api:{ip}", _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit          = generalLimit,
                            Window               = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow    = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit           = 0,
                        });

                // Root, favicon, or anything non-API — bypass
                return RateLimitPartition.GetNoLimiter("bypass");
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.Append("Retry-After", "60");
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsync(
                    """{"success":false,"message":"Rate limit exceeded. Please try again later.","data":null,"errors":["rate_limited"]}""",
                    cancellationToken);
            };
        });
    }

    var app = builder.Build();

    // Apply migrations and seed database (HTTP smoke factories disable this).
    var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
    if (applyMigrations)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }

    // Configure middleware pipeline
    // CORS must run early so preflight OPTIONS and error responses get ACAO headers.
    app.UseCors("AllowFrontend");

    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Request logging must be outer relative to exception handling so completion
    // status (including 499 client abort) is set before Serilog records the event.
    // Otherwise OperationCanceledException from client disconnect is logged as 500.
    var slowRequestMs = app.Configuration.GetValue("Logging:Performance:SlowRequestMs", 200);
    if (slowRequestMs < 0) slowRequestMs = 0;

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsedMs, ex) =>
        {
            // Client disconnect / request abort is expected (e.g. concurrent token refresh).
            if (ex is OperationCanceledException
                || httpContext.RequestAborted.IsCancellationRequested
                || httpContext.Response.StatusCode == ExceptionHandlingMiddleware.StatusClientClosedRequest)
                return LogEventLevel.Debug;

            if (ex is not null || httpContext.Response.StatusCode >= 500)
                return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400)
                return LogEventLevel.Warning;
            if (elapsedMs >= slowRequestMs)
                return LogEventLevel.Information;
            return LogEventLevel.Debug;
        };
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    });

    // After Serilog so handled exceptions surface as mapped status codes, not raw 500 + stack.
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Compress early so API JSON and (when applicable) downstream middleware benefit
    app.UseResponseCompression();

    // Image resize must run before static files (intercepts /uploads?w=&h=).
    app.UseMiddleware<ImageResizingMiddleware>();

    // ─── User-uploaded media — the only static files served from this API host ─────
    // The React SPA has moved to Cloudflare Pages CDN, so the API host no longer needs
    // to serve HTML/JS/CSS. Only /uploads (avatars, parking spot photos) is exposed.
    // Cloudflare R2 is the primary storage target in production; the local path is the
    // fallback for the InMemory storage provider (dev/test environments).
    var webRootPath = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    var uploadsPath  = Path.Combine(webRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider    = new PhysicalFileProvider(uploadsPath),
        RequestPath     = "/uploads",
        OnPrepareResponse = ctx =>
        {
            // 24-hour public cache for user-uploaded media.
            // Cloudflare R2 with a custom domain handles CDN caching in production.
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
        }
    });

    // Built-in .NET 9 rate limiter — replaces the in-process ConcurrentDictionary-based
    // RateLimitingMiddleware. The old middleware file is retained to avoid breaking any
    // unit tests that import its public static helpers (IsIotPath, ShouldSkipRateLimit).
    if (!rateLimitingDisabled)
    {
        app.UseRateLimiter();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    // Corporate tenant context first; channel matrix is authoritative allow/deny (KD-5).
    app.UseMiddleware<CorporateTenantMiddleware>();
    app.UseMiddleware<ChannelAuthorizationMiddleware>();

    app.MapControllers().RequireCors("AllowFrontend");

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
        .RequireCors("AllowFrontend");

    // Map SignalR hub for notifications
    app.MapHub<ParkingApp.Notifications.Infrastructure.Hubs.NotificationHub>("/hubs/notifications")
        .RequireCors("AllowFrontend");
    app.MapHub<ParkingApp.Messaging.Infrastructure.Hubs.ChatHub>("/hubs/chat")
        .RequireCors("AllowFrontend");

    // ─── API 404 fallback ────────────────────────────────────────────────────────────
    // The SPA is served from Cloudflare Pages — this API host has no index.html.
    // Unmatched routes return an instant structured JSON 404 with no disk I/O.
    // This is a significant performance gain: the old fallback read index.html from
    // disk on every unmatched request, blocking a thread-pool thread per hit.
    app.MapFallback(context =>
    {
        context.Response.StatusCode  = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(
            """{"success":false,"message":"API endpoint not found","data":null,"errors":["Not Found"]}""");
    });

    app.Run();
}
// HostAbortedException is thrown by WebApplicationFactory / HostFactoryResolver to stop the
// entry point after the host is captured. Swallowing it causes:
// "The entry point exited without ever building an IHost."
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
