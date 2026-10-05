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
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class ControlledTimeJobsBehaviorHttpTests : IDisposable
{
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;

    public ControlledTimeJobsBehaviorHttpTests(FullApiPostgresFixture postgres)
    {
        _factory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
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

    // ==========================================
    // SESSION REMINDER: LEGACY PATH vs HTTP PATH
    // ==========================================

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsBehavior")]
    public async Task CJ5_SessionReminder_LegacyPath_EquivalenceTest()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj5");

        // Advance to 15 mins before end
        _factory.Clock.SetUtcNow(end.AddMinutes(-15));

        // LEGACY PATH: direct execution of application service
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ISessionReminderService>();
            var result = await service.ProcessAsync(10);
            result.Notified.Should().BeGreaterThanOrEqualTo(1);
        }

        // Validate state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.SessionEndRemindedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingSessionEndRemindedEvent")).ToListAsync();
            outbox.Should().NotBeEmpty();
        }
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsBehavior")]
    public async Task CJ6_SessionReminder_HttpPath_EquivalenceTest()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj6");

        // Advance to 15 mins before end
        _factory.Clock.SetUtcNow(end.AddMinutes(-15));

        // HTTP PATH: execution of endpoint
        var response = await _client.PostAsync("/api/internal/jobs/sessions/remind?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Validate state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.SessionEndRemindedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingSessionEndRemindedEvent")).ToListAsync();
            outbox.Should().NotBeEmpty();
        }
    }

    // ==========================================
    // OVERSTAY DETECTION: LEGACY PATH vs HTTP PATH
    // ==========================================

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsBehavior")]
    public async Task CJ7_Overstay_LegacyPath_EquivalenceTest()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj7");

        // Legitimate check-in workflow
        _factory.Clock.SetUtcNow(end.AddMinutes(-120)); // Within check-in window
        var checkIn = await _client.PostAsync($"/api/bookings/{bookingId}/check-in", null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK, because: await checkIn.Content.ReadAsStringAsync());

        // Advance past end time + grace period
        _factory.Clock.SetUtcNow(end.AddMinutes(30));

        // LEGACY PATH: direct execution of application service
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IOverstayDetectionService>();
            var result = await service.ProcessAsync(10);
            result.Notified.Should().BeGreaterThanOrEqualTo(1);
        }

        // Validate state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.OverstayNotifiedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingOverstayNotifiedEvent")).ToListAsync();
            outbox.Should().NotBeEmpty();
        }
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsBehavior")]
    public async Task CJ8_Overstay_HttpPath_EquivalenceTest()
    {
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj8");

        // Legitimate check-in workflow
        _factory.Clock.SetUtcNow(end.AddMinutes(-120)); // Within check-in window
        var checkIn = await _client.PostAsync($"/api/bookings/{bookingId}/check-in", null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK, because: await checkIn.Content.ReadAsStringAsync());

        // Advance past end time + grace period
        _factory.Clock.SetUtcNow(end.AddMinutes(30));

        // HTTP PATH: execution of endpoint
        var response = await _client.PostAsync("/api/internal/jobs/overstays/detect?batchSize=10", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Validate state
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FindAsync(bookingId);
            booking!.OverstayNotifiedAt.Should().NotBeNull();
            
            var outbox = await db.OutboxMessages.Where(m => m.TypeName.Contains("BookingOverstayNotifiedEvent")).ToListAsync();
            outbox.Should().NotBeEmpty();
        }
    }

    // ==========================================
    // OUTBOX LIFECYCLE TEST
    // ==========================================

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "ControlledTimeJobsBehavior")]
    public async Task CJ9_Outbox_Lifecycle_Test()
    {
        // Prove that an action generates an event, persists it in the Outbox, and it can be processed
        var (bookingId, end) = await SeedPaidConfirmedBookingAsync("cj9");

        _factory.Clock.SetUtcNow(end.AddMinutes(-120)); // Within check-in window
        var checkIn = await _client.PostAsync($"/api/bookings/{bookingId}/check-in", null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK, because: await checkIn.Content.ReadAsStringAsync());

        // Wait for OutboxProcessor (Hosted Service) to pick it up and process it
        bool processed = false;
        for (int i = 0; i < 20; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var msg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.TypeName.Contains("BookingCheckedInEvent") && m.Payload.Contains(bookingId.ToString()));
            
            if (msg != null && msg.Status == ParkingApp.Infrastructure.Outbox.OutboxStatus.Processed)
            {
                processed = true;
                break;
            }
            await Task.Delay(200);
        }

        processed.Should().BeTrue("The outbox processor should have consumed and processed the message.");
    }

    // ==========================================
    // HELPERS
    // ==========================================

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
