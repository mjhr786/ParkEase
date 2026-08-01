using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddNotificationServices(builder.Configuration);

    builder.Services.AddApplication();
    builder.Services.AddCorporateApplication();
    builder.Services.AddIdentityApplication();
    builder.Services.AddMarketplaceApplication(builder.Configuration);
    builder.Services.AddMessagingApplication();
    builder.Services.AddAdminApplication();

    // Add Controllers
    builder.Services.AddControllers();

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
        };

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
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

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Apply migrations and seed database
    using (var scope = app.Services.CreateScope())
    {
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

    // Static files for uploads with caching
    var webRootPath = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(webRootPath);

    // Serve default files (index.html) for SPA
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webRootPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            // Cache static assets with hashes indefinitely, but NEVER cache index.html
            if (ctx.Context.Request.Path.Value?.EndsWith(".html") == true)
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                ctx.Context.Response.Headers.Append("Expires", "0");
            }
            else if (ctx.Context.Request.Path.Value?.Contains("/assets/") == true)
            {
                // Vite assets have content hashes in their filenames, so they can be cached safely for a long time
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000,immutable");
            }
            else
            {
                // Shorter cache for other static files (images, etc)
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
            }
        }
    });

    // Rate-limit API (and SPA fallback) only - after static files so /assets never burns the budget.
    // Middleware also skips /health, /hubs, /assets, /uploads, common static extensions.
    app.UseMiddleware<RateLimitingMiddleware>();

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

    // SPA fallback — last. Never serve index.html for API/hubs/health (DEF-002):
    // otherwise missing or unmatched /api/* routes return 200 text/html and break clients
    // (e.g. access-pass expecting JSON).
    app.MapFallback(async context =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api")
            || path.StartsWithSegments("/hubs")
            || path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync(
                """{"success":false,"message":"API endpoint not found","data":null,"errors":["Not Found"]}""");
            return;
        }

        var index = new PhysicalFileProvider(webRootPath).GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        await context.Response.SendFileAsync(index);
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
