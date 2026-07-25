using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingApp.Notifications.Contracts;

using ParkingApp.Notifications.Application.Services;
using ParkingApp.Notifications.Application.Queries.Notifications;
using ParkingApp.Notifications.Infrastructure.Services;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Notifications.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Modular monolith composition entry point for the Notifications module.
    /// Prefer this over <see cref="AddNotificationServices"/> in new composition roots.
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddNotificationServices(configuration);

    /// <summary>
    /// Notifications module: delivery channels, email, and CQRS handlers.
    /// </summary>
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // SignalR Real-time Notifications
        services.AddScoped<INotificationService, SignalRNotificationService>();

        // External Notification Services (SMS/Push)
        services.AddScoped<ISmsNotificationService, MockSmsNotificationService>();

        // Email delivery (BuildingBlocks IEmailService — used by Notifications + Corporate invitations)
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // Initialize Firebase Admin SDK (singleton — do this once at startup)
        InitializeFirebase(configuration);

        // Real FCM push notification service
        services.AddScoped<IPushNotificationService, FirebasePushNotificationService>();

        // Off-request-path push (chat etc.) — singleton scheduler, creates scopes per job
        services.AddSingleton<IDeferredPushNotificationService, DeferredPushNotificationService>();

        // Notification Coordinator (orchestrates all channels)
        services.AddScoped<INotificationCoordinator, NotificationCoordinator>();

        // Register CQRS handlers
        services.AddHandlersFromAssembly(typeof(GetMyNotificationsQueryHandler).Assembly, throwIfMissingHandlers: false);

        return services;
    }

    private static void InitializeFirebase(IConfiguration configuration)
    {
        // Only initialize once
        if (FirebaseApp.DefaultInstance != null)
            return;

        try
        {
            // Build the service account JSON from individual config keys
            // (avoids storing the raw private key in a single config blob)
            var projectId = configuration["Firebase:ProjectId"];
            var clientEmail = configuration["Firebase:ClientEmail"];
            var privateKeyId = configuration["Firebase:PrivateKeyId"];
            var privateKey = configuration["Firebase:PrivateKey"]
                ?.Replace("\\n", "\n"); // handle escaped newlines from env vars

            if (string.IsNullOrWhiteSpace(projectId) ||
                string.IsNullOrWhiteSpace(clientEmail) ||
                string.IsNullOrWhiteSpace(privateKey))
            {
                // No credentials configured ΓÇö fall back to mock in development
                Console.WriteLine(">> Firebase credentials not configured ΓÇö push notifications will be mocked");
                return;
            }

            var serviceAccountJson = $@"{{
  ""type"": ""service_account"",
  ""project_id"": ""{projectId}"",
  ""private_key_id"": ""{privateKeyId}"",
  ""private_key"": ""{privateKey.Replace("\n", "\\n")}"",
  ""client_email"": ""{clientEmail}"",
  ""client_id"": ""{configuration["Firebase:ClientId"]}"",
  ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
  ""token_uri"": ""https://oauth2.googleapis.com/token"",
  ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
  ""client_x509_cert_url"": ""{configuration["Firebase:ClientX509CertUrl"]}"",
  ""universe_domain"": ""googleapis.com""
}}";

            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential
                    .FromJson(serviceAccountJson)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
            });

            Console.WriteLine($">> Firebase Admin SDK initialized for project: {projectId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($">> Firebase initialization failed: {ex.Message}");
            // Non-fatal ΓÇö app still runs, push notifications will fail gracefully
        }
    }
}
