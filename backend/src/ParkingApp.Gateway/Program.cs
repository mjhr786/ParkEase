using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add YARP reverse proxy
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add rate limiting
var permitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue<int>("RateLimiting:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Fixed window rate limiter per IP
    options.AddPolicy("fixed", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // Sliding window for authenticated users (more lenient)
    options.AddPolicy("sliding", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit * 2, // Double for authenticated
                Window = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Too many requests. Please try again later."
        }, token);
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCors", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:3000",
            "http://127.0.0.1:5173"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure middleware pipeline
app.UseRateLimiter();
app.UseCors("GatewayCors");

// Gateway health check
app.MapGet("/gateway/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    service = "API Gateway",
    timestamp = DateTime.UtcNow 
}));

// Map reverse proxy with rate limiting
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use((context, next) =>
    {
        // Log incoming requests
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Gateway routing: {Method} {Path}", 
            context.Request.Method, context.Request.Path);
        return next();
    });
}).RequireRateLimiting("fixed");

app.Run();
