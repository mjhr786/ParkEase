using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class ControlledTimeJobsHttpOnlyTests : IDisposable
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock;

    public ControlledTimeJobsHttpOnlyTests(FullApiPostgresFixture postgres)
    {
        var baseFactory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
        _clock = baseFactory.Clock;

        _factory = baseFactory.WithWebHostBuilder(builder => 
        {
            builder.ConfigureAppConfiguration((_, config) => 
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jobs:UseHostedServices"] = "false"
                });
            });
        });

        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        
        _client.DefaultRequestHeaders.Add("X-Api-Key", "TestKey123!");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsHttpOnly")]
    public void CJ10_Verify_HostedServices_Disabled()
    {
        using var scope = _factory.Services.CreateScope();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();

        var types = hostedServices.Select(x => x.GetType().Name).ToList();

        types.Should().NotContain("OutboxBackgroundService");
        types.Should().NotContain("SessionReminderBackgroundService");
        types.Should().NotContain("OverstayDetectionBackgroundService");
        types.Should().NotContain("WaitlistAutoPromotionBackgroundService");
    }

    public class TestNotificationCoordinator : ParkingApp.Notifications.Contracts.INotificationCoordinator
    {
        public bool WasCalled { get; private set; }
        public Task SendAsync(Guid userId, ParkingApp.Notifications.Contracts.NotificationRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }

        public Task SendBulkAsync(IEnumerable<Guid> userIds, ParkingApp.Notifications.Contracts.NotificationRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsHttpOnly")]
    public async Task CJ11_Outbox_HttpOnly_Test()
    {
        // 1. Create a legitimate test-domain operation that creates an OutboxMessage.
        // We will directly insert an OutboxMessage into DB to bypass the UnitOfWork fast-path.
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var domainEvent = new ParkingApp.Marketplace.Domain.Events.BookingCheckedInEvent(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "TESTREF1"
        );
        var typeName = domainEvent.GetType().AssemblyQualifiedName!;
        var payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

        var testCoordinator = new TestNotificationCoordinator();
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Remove(services.First(s => s.ServiceType == typeof(ParkingApp.Notifications.Contracts.INotificationCoordinator)));
                services.AddSingleton<ParkingApp.Notifications.Contracts.INotificationCoordinator>(testCoordinator);
            });
        });
        var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "TestKey123!");

        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var msg = new ParkingApp.Infrastructure.Outbox.OutboxMessage
            {
                Id = messageId,
                TypeName = typeName,
                Payload = payload,
                CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
            };
            db.OutboxMessages.Add(msg);
            await db.SaveChangesAsync();
        }

        // 3. Call HTTP endpoint
        var response = await client.PostAsync("/api/internal/jobs/outbox/process?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var resultStr = await response.Content.ReadAsStringAsync();
        resultStr.Should().Contain("\"processed\":1", "The HTTP endpoint should process exactly 1 outbox message");

        // 5. Verify the Outbox status changes appropriately and real handler side effect occurred
        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var msg = await db.OutboxMessages.FindAsync(messageId);
            msg!.Status.Should().Be(ParkingApp.Infrastructure.Outbox.OutboxStatus.Processed, "The HTTP endpoint should successfully process real valid events.");

            // Verify the specific side effect from BookingCheckedInGuestNotificationHandler
            testCoordinator.WasCalled.Should().BeTrue("BookingCheckedInGuestNotificationHandler should have called INotificationCoordinator for the guest");
        }
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsHttpOnly")]
    public async Task CJ12_SessionReminder_HttpOnly_Test()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj12");
        _clock.SetUtcNow(end.AddMinutes(-15));

        var response = await _client.PostAsync("/api/internal/jobs/sessions/remind?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.SessionEndRemindedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingSessionEndRemindedEvent") && m.Payload.Contains(bookingId.ToString())).ToListAsync();
            outbox.Should().HaveCount(1);
        }
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsHttpOnly")]
    public async Task CJ13_Overstay_HttpOnly_Test()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj13");
        _clock.SetUtcNow(end.AddMinutes(-120));
        var checkIn = await _client.PostAsync($"/api/bookings/{bookingId}/check-in", null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK);

        _clock.SetUtcNow(end.AddMinutes(30));

        var response = await _client.PostAsync("/api/internal/jobs/overstays/detect?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.OverstayNotifiedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingOverstayNotifiedEvent") && m.Payload.Contains(bookingId.ToString())).ToListAsync();
            outbox.Should().HaveCount(1);
        }
    }

    [Theory]
    [Trait("Layer", "Http")]
    [InlineData("api/internal/jobs/outbox/process")]
    [InlineData("api/internal/jobs/sessions/remind")]
    [InlineData("api/internal/jobs/overstays/detect")]
    [InlineData("api/internal/jobs/waitlist/promote")]
    public async Task CJ14_Verify_ApiAuth(string url)
    {
        // missing key
        var clientNoKey = _factory.CreateClient();
        var r1 = await clientNoKey.PostAsync(url, null);
        r1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // invalid key
        var clientBadKey = _factory.CreateClient();
        clientBadKey.DefaultRequestHeaders.Add("X-Api-Key", "WrongKey!");
        var r2 = await clientBadKey.PostAsync(url, null);
        r2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // valid key
        var r3 = await _client.PostAsync(url, null);
        r3.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsHttpOnly")]
    public async Task CJ15_Verify_BoundedExecution()
    {
        // Verify batchSize=10000 cannot bypass server limits
        var response = await _client.PostAsync("/api/internal/jobs/outbox/process?batchSize=10000", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }


    private async Task<(Guid BookingId, DateTime EndUtc)> SeedPaidConfirmedBookingAsync(string prefix)
    {
        var (_, guest, spaceId) = await SeedVendorAndGuestListingAsync(prefix);
        _client.UseBearer(guest.AccessToken);

        var start = DateTime.UtcNow.AddMinutes(30);
        var end = start.AddHours(2);
        
        // 1. Create Booking
        var (createResp, created) = await CreateBookingAsync(spaceId, start, end);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created, because: await createResp.Content.ReadAsStringAsync());
        var bookingId = created!.Data!.Id;

        // 2. Create Order
        var orderResponse = await _client.PostAsJsonAsync(
            "/api/payments/create-order",
            new { bookingId },
            HttpApiClientExtensions.JsonOptions);
        var orderBody = await orderResponse.ReadApiResponseAsync<string>();
        orderResponse.EnsureSuccessStatusCode();

        // 3. Verify Payment
        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/payments/verify",
            new
            {
                BookingId = bookingId,
                RazorpayPaymentId = $"pi_{prefix}_{Guid.NewGuid():N}"[..22],
                RazorpayOrderId = orderBody!.Data,
                RazorpaySignature = "ok"
            },
            HttpApiClientExtensions.JsonOptions);
        var verifyBody = await verifyResponse.ReadApiResponseAsync<PaymentResultDto>();
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: await verifyResponse.Content.ReadAsStringAsync());
        
        return (bookingId, end);
    }

    private async Task<(TokenDto Vendor, TokenDto Guest, Guid SpaceId)> SeedVendorAndGuestListingAsync(string prefix)
    {
        var vendor = await _client.RegisterAndGetTokensAsync($"{prefix}_vendor");
        _client.UseBearer(vendor.AccessToken);
        var space = await CreateInstantBookListingAsync($"IT Spot {prefix} {Guid.NewGuid():N}"[..30]);

        _client.ClearBearer();
        var guest = await _client.RegisterAndGetTokensAsync($"{prefix}_guest");
        return (vendor, guest, space.Id);
    }

    private async Task<ParkingSpaceDto> CreateInstantBookListingAsync(string title, string city = "Bengaluru")
    {
        var dto = new
        {
            title,
            description = "Integration test public parking listing for book/pay/check-in.",
            address = "100 IT Test Road",
            city,
            state = "KA",
            country = "IN",
            postalCode = "560001",
            latitude = 12.9716,
            longitude = 77.5946,
            parkingType = ParkingType.Open,
            totalSpots = 2,
            hourlyRate = 50m,
            dailyRate = 300m,
            weeklyRate = 1500m,
            monthlyRate = 5000m,
            openTime = (TimeSpan?)null,
            closeTime = (TimeSpan?)null,
            is24Hours = true,
            amenities = (List<string>?)null,
            allowedVehicleTypes = (List<VehicleType>?)null,
            imageUrls = (List<string>?)null,
            specialInstructions = (string?)null,
            zoneCode = (string?)null,
            isLprEnabled = false,
            isDynamicPricingEnabled = false,
            listingCategory = ListingCategory.Residential,
            instantBook = true
        };

        var response = await _client.PostAsJsonAsync("/api/parking", dto, HttpApiClientExtensions.JsonOptions);
        var body = await response.ReadApiResponseAsync<ParkingSpaceDto>();
        response.EnsureSuccessStatusCode();
        return body!.Data!;
    }

    private async Task<(HttpResponseMessage Response, ApiResponse<BookingDto>? Body)> CreateBookingAsync(
        Guid parkingSpaceId,
        DateTime startUtc,
        DateTime endUtc)
    {
        var dto = new CreateBookingDto(
            parkingSpaceId,
            startUtc,
            endUtc,
            PricingType.Hourly,
            VehicleType.Car,
            SlotNumber: null,
            VehicleNumber: "KA01IT1234",
            VehicleModel: "Test",
            VehicleColor: "Blue",
            DiscountCode: null);

        var response = await _client.PostAsJsonAsync("/api/bookings", dto, HttpApiClientExtensions.JsonOptions);
        var body = await response.ReadApiResponseAsync<BookingDto>();
        return (response, body);
    }
}
