using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Application.DTOs;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class ControlledTimeJobsHttpTests : IDisposable
{
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;

    public ControlledTimeJobsHttpTests(FullApiPostgresFixture postgres)
    {
        _factory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        // Give internal jobs endpoint access
        _client.DefaultRequestHeaders.Add("X-Api-Key", "TestKey123!");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobs")]
    public async Task CJ1_SharedClock_Between_HTTP_And_BackgroundService()
    {
        // Prove that the factory's clock is the one resolved by DI (used by both paths)
        var resolvedTimeProvider = _factory.Services.GetRequiredService<TimeProvider>();
        resolvedTimeProvider.Should().BeSameAs(_factory.Clock);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobs")]
    public async Task CJ2_SessionReminderEndpoint_UsesTimeProvider_WithoutCrashing()
    {
        // Prove DI works and endpoint returns success
        _factory.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var response = await _client.PostAsync("/api/internal/jobs/sessions/remind?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobs")]
    public async Task CJ3_OverstayEndpoint_UsesTimeProvider_WithoutCrashing()
    {
        // Prove DI works and endpoint returns success
        _factory.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var response = await _client.PostAsync("/api/internal/jobs/overstays/detect?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobs")]
    public async Task CJ4_WaitlistEndpoint_UsesTimeProvider_WithoutCrashing()
    {
        // Prove DI works and endpoint returns success
        _factory.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var response = await _client.PostAsync("/api/internal/jobs/waitlist/promote?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null, 50)]     // Default
    [InlineData(100, 100)]     // Normal value
    [InlineData(200, 200)]     // Maximum
    [InlineData(201, 200)]     // Above maximum
    [InlineData(10000, 200)]   // Large malicious/unbounded request
    [InlineData(0, 1)]         // Negative/Low value (minimum clamped to 1)
    [InlineData(-50, 1)]       // Negative/Low value
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobs")]
    public async Task CJ5_OutboxEndpoint_EnforcesBatchLimits(int? requestedBatchSize, int expectedProcessedCount)
    {
        var testType = $"DummyLimitTest_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (int i = 0; i < 205; i++)
            {
                db.OutboxMessages.Add(new ParkingApp.Infrastructure.Outbox.OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    TypeName = testType,
                    Payload = "{}",
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    Status = ParkingApp.Infrastructure.Outbox.OutboxStatus.Pending,
                    CreatedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
            }
            await db.SaveChangesAsync();
        }

        try
        {
            // Test boundary limits
            string url = requestedBatchSize.HasValue 
                ? $"/api/internal/jobs/outbox/process?batchSize={requestedBatchSize.Value}"
                : "/api/internal/jobs/outbox/process";
                
            var response = await _client.PostAsync(url, null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify the bounded claim
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Messages processed (or attempted to process) will have AttemptCount > 0
                var processed = await db.OutboxMessages.CountAsync(m => m.TypeName == testType && m.AttemptCount > 0);
                
                var json = await response.Content.ReadAsStringAsync();
                processed.Should().Be(expectedProcessedCount, $"Because batch size {requestedBatchSize} should be strictly clamped at the boundary to {expectedProcessedCount}. JSON: {json}");
            }
        }
        finally
        {
            // Clean up to prevent affecting next theory runs
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.OutboxMessages.Where(m => m.TypeName == testType).ExecuteDeleteAsync();
            }
        }
    }
}
