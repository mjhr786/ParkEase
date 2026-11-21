using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Infrastructure.Data;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class ExpireAwaitingPaymentsHttpTests : IDisposable
{
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;

    public ExpireAwaitingPaymentsHttpTests(FullApiPostgresFixture postgres)
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

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task InstantBook_NoPaymentOrder_IsNotExpired()
    {
        var bookingId = await SeedInstantBookAwaitingAsync("ex_none");

        var response = await PostExpireAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status.Should().Be(BookingStatus.AwaitingPayment);
        var body = await ReadJobAsync(response);
        body.ExpiredBookings.Should().Be(0);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task InstantBook_ExpiredUpi_GraceElapsed_ExpiresBooking()
    {
        var bookingId = await SeedInstantBookAwaitingAsync("ex_upi");
        var now = _factory.Clock.GetUtcNow().UtcDateTime;
        await SeedOrderAsync(
            bookingId,
            PaymentOrder.RailUpi,
            PaymentOrder.ProviderRazorpay,
            "order_exp_" + Guid.NewGuid().ToString("N")[..8],
            "pay_exp_" + Guid.NewGuid().ToString("N")[..8],
            PaymentOrderStatus.Expired,
            now.AddMinutes(-40));

        var response = await PostExpireAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status.Should().Be(BookingStatus.Expired);
        (await ReadJobAsync(response)).ExpiredBookings.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task InstantBook_PriorUpiExpired_LiveStripeCreated_IsNotExpired()
    {
        var bookingId = await SeedInstantBookAwaitingAsync("ex_live");
        var now = _factory.Clock.GetUtcNow().UtcDateTime;
        await SeedOrderAsync(
            bookingId,
            PaymentOrder.RailUpi,
            PaymentOrder.ProviderRazorpay,
            "order_old_" + Guid.NewGuid().ToString("N")[..8],
            "pay_old_" + Guid.NewGuid().ToString("N")[..8],
            PaymentOrderStatus.Expired,
            now.AddMinutes(-40));
        await SeedOrderAsync(
            bookingId,
            PaymentOrder.RailStripe,
            PaymentOrder.ProviderStripe,
            "pi_live_" + Guid.NewGuid().ToString("N")[..8],
            "pi_live_" + Guid.NewGuid().ToString("N")[..8],
            PaymentOrderStatus.Created,
            now.AddMinutes(10));

        var response = await PostExpireAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status.Should().Be(BookingStatus.AwaitingPayment);
    }

    private async Task<HttpResponseMessage> PostExpireAsync()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        return await _client.PostAsync("/api/internal/jobs/payments/expire-awaiting?batchSize=50", content: null);
    }

    private static async Task<ExpireAwaitingPaymentsResult> ReadJobAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ExpireAwaitingPaymentsResult>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private async Task<Guid> SeedInstantBookAwaitingAsync(string prefix)
    {
        var vendor = await _client.RegisterAndGetTokensAsync($"{prefix}_vendor");
        _client.UseBearer(vendor.AccessToken);
        var space = await CreateInstantBookListingAsync($"IT Spot {prefix} {Guid.NewGuid():N}"[..30]);
        _client.ClearBearer();
        var guest = await _client.RegisterAndGetTokensAsync($"{prefix}_guest");
        _client.UseBearer(guest.AccessToken);
        var start = DateTime.UtcNow.AddMinutes(30);
        var (createResp, created) = await CreateBookingAsync(space.Id, start, start.AddHours(2));
        createResp.EnsureSuccessStatusCode();
        created!.Data!.Status.Should().Be(BookingStatus.AwaitingPayment);
        return created.Data.Id;
    }

    private async Task SeedOrderAsync(
        Guid bookingId,
        string rail,
        string provider,
        string orderId,
        string payId,
        PaymentOrderStatus status,
        DateTime expiresAtUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.PaymentOrders.Add(PaymentOrder.Create(
            bookingId,
            "booking",
            rail,
            provider,
            orderId,
            50m,
            "INR",
            status,
            payId,
            expiresAtUtc));
        await db.SaveChangesAsync();
    }

    private async Task<ParkingSpaceDto> CreateInstantBookListingAsync(string title)
    {
        var dto = new
        {
            title,
            description = "Expire-job integration listing.",
            address = "100 IT Test Road",
            city = "Bengaluru",
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
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: await response.Content.ReadAsStringAsync());
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
            VehicleNumber: "KA01EX1234",
            VehicleModel: "Test",
            VehicleColor: "Blue",
            DiscountCode: null);

        var response = await _client.PostAsJsonAsync("/api/bookings", dto, HttpApiClientExtensions.JsonOptions);
        var body = await response.ReadApiResponseAsync<BookingDto>();
        return (response, body);
    }
}
