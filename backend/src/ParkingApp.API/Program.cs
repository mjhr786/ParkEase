using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using ParkingApp.API.Middleware;
using ParkingApp.Application;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Infrastructure;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Infrastructure and Application layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Register FileUploadService
var uploadsPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadsPath);
builder.Services.AddScoped<IFileUploadService>(sp =>
{
    var unitOfWork = sp.GetRequiredService<ParkingApp.Domain.Interfaces.IUnitOfWork>();
    return new FileUploadService(unitOfWork, uploadsPath);
});

// Add SignalR for real-time notifications
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

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
    options.RequireHttpsMetadata = false; // Set to true in production
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

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
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

// Apply migrations and seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

// Configure middleware pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseCors("AllowFrontend");

// Static files for uploads with caching
var webRootPath = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath),
    RequestPath = "",
    OnPrepareResponse = ctx =>
    {
        // Cache images/videos for 7 days
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800");
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// SignalR Hub endpoint
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

