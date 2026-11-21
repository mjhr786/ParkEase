using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Infrastructure.Data;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class RazorpayWebhookHttpTests : IDisposable
{
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;

    public RazorpayWebhookHttpTests(FullApiPostgresFixture postgres)
    {
        _factory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task Hmac_ValidAndInvalidCases()
    {
        var json = """{"event":"qr_code.credited","payload":{"payment":{"entity":{"id":"pay_hmac"}}}}""";
        var body = Encoding.UTF8.GetBytes(json);

        (await PostWebhookAsync(body, "evt_hmac_valid")).StatusCode.Should().Be(HttpStatusCode.OK);

        var truncated = body.AsSpan(0, Math.Max(1, body.Length / 2)).ToArray();
        var truncatedReq = await PostWebhookAsync(truncated, "evt_hmac_trunc", signBody: body);
        truncatedReq.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var odd = await SendRawAsync(body, signature: "abc", eventId: "evt_odd");
        odd.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var empty = await SendRawAsync(Array.Empty<byte>(), Sign(body), "evt_empty");
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var wrong = await SendRawAsync(body, RazorpayWebhookSignature.ComputeHex(body, "wrong_secret"), "evt_wrong");
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missing = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/razorpay")
        {
            Content = new ByteArrayContent(body)
        };
        missing.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        missing.Headers.Add(RazorpayWebhookSignature.EventIdHeaderName, "evt_missing");
        (await _client.SendAsync(missing)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task Hmac_RawBodyWithExtraWhitespace_Succeeds()
    {
        var json = """{ "event" : "qr_code.credited" , "payload" : { "payment" : { "entity" : { "id" : "pay_ws" } } } }""";
        var response = await PostWebhookAsync(Encoding.UTF8.GetBytes(json), "evt_ws");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadOutcomeAsync(response)).Should().Be("ignored_unhandled");
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task UnknownProvider_Paypal_Returns404()
    {
        var body = Encoding.UTF8.GetBytes("""{"event":"payment.captured"}""");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/paypal")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add(RazorpayWebhookSignature.SignatureHeaderName, Sign(body));
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task StripeProvider_MissingSignature_Returns400()
    {
        var body = Encoding.UTF8.GetBytes("""{"type":"payment_intent.succeeded"}""");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task ConcurrentCapturedAndOrderPaid_OneConfirmZeroRefunds()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_cc");
        var captured = CapturePayload("evt_cc_cap", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor, "payment.captured");
        var paid = CapturePayload("evt_cc_paid", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor, "order.paid");

        var first = PostWebhookAsync(captured.Body, captured.EventId);
        var second = PostWebhookAsync(paid.Body, paid.EventId);
        var results = await Task.WhenAll(first, second);

        for (var i = 0; i < results.Length; i++)
        {
            var response = results[i];
            var body = i == 0 ? captured.Body : paid.Body;
            var eventId = i == 0 ? captured.EventId : paid.EventId;
            for (var attempt = 0; attempt < 8 && response.StatusCode == HttpStatusCode.ServiceUnavailable; attempt++)
            {
                await Task.Delay(250);
                response.Dispose();
                response = await PostWebhookAsync(body, eventId);
            }

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.PaymentWebhookEvents.CountAsync(e => e.ProviderEventId == captured.EventId || e.ProviderEventId == paid.EventId))
            .Should().Be(2);
        (await db.Payments.CountAsync(p => p.BookingId == ctx.BookingId)).Should().Be(1);
        var booking = await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId);
        booking.Status.Should().Be(BookingStatus.Confirmed);
        _factory.UpiCheckoutService.Refunds.Should().BeEmpty();
        (await db.PaymentWebhookEvents.CountAsync(e =>
            (e.ProviderEventId == captured.EventId || e.ProviderEventId == paid.EventId)
            && (e.Outcome == "needs_refund" || e.Outcome == "refunded")))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task CancelledOrder_WhileBookingAwaiting_Revives()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_rv");
        await MutateOrderAsync(ctx.OrderId, o => o.Cancel());

        var payload = CapturePayload("evt_rv", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, payload.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        (await ReadOutcomeAsync(response)).Should().Be("captured_revived");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.Confirmed);
        (await db.PaymentOrders.SingleAsync(o => o.ProviderOrderId == ctx.OrderId)).Status.Should().Be(PaymentOrderStatus.Captured);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task AuthorizedSnapshot_DoesNotConfirm()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_auth", status: "authorized");
        var payload = CapturePayload("evt_auth", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, payload.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadOutcomeAsync(response)).Should().Be("ignored_not_captured");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.AwaitingPayment);
        (await db.Payments.CountAsync(p => p.BookingId == ctx.BookingId)).Should().Be(0);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task MatcherMismatch_NeedsRefund_NotConfirmed()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_mm");
        var mismatchedBookingId = Guid.NewGuid();
        ConfigureSnapshot(ctx.PayId, ctx.OrderId, mismatchedBookingId, ctx.AmountMinor, ctx.Amount, "booking");
        var payload = CapturePayload("evt_mm", ctx.PayId, ctx.OrderId, mismatchedBookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, payload.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadOutcomeAsync(response)).Should().Be("needs_refund");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.AwaitingPayment);
        (await db.Payments.CountAsync(p => p.BookingId == ctx.BookingId)).Should().Be(0);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task OutcomeB_CancelledBooking_RefundsOrNeedsRefund()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_b");
        await MutateBookingAsync(ctx.BookingId, b => b.Cancel("changed plans"));

        var payload = CapturePayload("evt_b", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, payload.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var outcome = await ReadOutcomeAsync(response);
        outcome.Should().BeOneOf("refunded", "needs_refund");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.Cancelled);
        (await db.Payments.CountAsync(p => p.BookingId == ctx.BookingId)).Should().Be(0);
        if (outcome == "refunded")
            _factory.UpiCheckoutService.Refunds.Should().Contain(r => r.ProviderPaymentId == ctx.PayId);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task OutcomeC_DifferentPayId_RefundsNewChargeOnly()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_c");
        var first = CapturePayload("evt_c1", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        (await PostWebhookAsync(first.Body, first.EventId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPay = "pay_c2_" + Guid.NewGuid().ToString("N")[..10];
        ConfigureSnapshot(secondPay, ctx.OrderId, ctx.BookingId, ctx.AmountMinor, ctx.Amount, "booking");
        var second = CapturePayload("evt_c2", secondPay, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(second.Body, second.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var outcome = await ReadOutcomeAsync(response);
        outcome.Should().BeOneOf("refunded", "needs_refund");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.BookingId == ctx.BookingId);
        payment.TransactionId.Should().Be(ctx.PayId);
        (await db.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.Confirmed);
        if (outcome == "refunded")
            _factory.UpiCheckoutService.Refunds.Should().Contain(r => r.ProviderPaymentId == secondPay);
        _factory.UpiCheckoutService.Refunds.Should().NotContain(r => r.ProviderPaymentId == ctx.PayId);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task ExtensionWebhook_DoesNotOverwritePrimaryTransactionId()
    {
        var prefix = "wh_ext";
        var (_, guest, spaceId) = await SeedVendorAndGuestListingAsync(prefix);
        _client.UseBearer(guest.AccessToken);
        var start = DateTime.UtcNow.AddMinutes(30);
        var (createResp, created) = await CreateBookingAsync(spaceId, start, start.AddHours(2));
        createResp.EnsureSuccessStatusCode();
        var bookingId = created!.Data!.Id;

        var orderResponse = await _client.PostAsJsonAsync(
            "/api/payments/create-order", new { bookingId }, HttpApiClientExtensions.JsonOptions);
        var orderBody = await orderResponse.ReadApiResponseAsync<string>();
        orderResponse.EnsureSuccessStatusCode();
        var originalPi = $"pi_{prefix}_{Guid.NewGuid():N}"[..22];
        var verify = await _client.PostAsJsonAsync(
            "/api/payments/verify",
            new VerifyPaymentDto
            {
                BookingId = bookingId,
                RazorpayPaymentId = originalPi,
                RazorpayOrderId = orderBody!.Data,
                RazorpaySignature = "test_sig"
            },
            HttpApiClientExtensions.JsonOptions);
        verify.EnsureSuccessStatusCode();

        const decimal extAmount = 40m;
        await MutateBookingAsync(bookingId, b =>
        {
            b.RequestExtension(b.EndDateTime.AddHours(1), extAmount);
            b.ApproveExtension();
        });

        var payExt = "pay_ext_" + Guid.NewGuid().ToString("N")[..10];
        var orderExt = "order_ext_" + Guid.NewGuid().ToString("N")[..10];
        var amountMinor = ToMinor(extAmount);
        await SeedPaymentOrderAsync(bookingId, "extension", orderExt, payExt, extAmount);
        ConfigureSnapshot(payExt, orderExt, bookingId, amountMinor, extAmount, "extension");

        var payload = CapturePayload("evt_ext", payExt, orderExt, bookingId, amountMinor, purpose: "extension");
        var response = await PostWebhookAsync(payload.Body, payload.EventId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadOutcomeAsync(response)).Should().Be("captured");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.ExtensionFeeTransactionId.Should().Be(payExt);
        var payment = await db.Payments.SingleAsync(p => p.BookingId == bookingId);
        payment.TransactionId.Should().Be(originalPi);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task LockNotAcquired_Returns503RetryAfter()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_lk");
        using (var scope = _factory.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            (await cache.AcquireLockAsync(PaymentLock.Key(ctx.BookingId, "booking"), TimeSpan.FromMinutes(1)))
                .Should().BeTrue();
        }

        var payload = CapturePayload("evt_lk", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, payload.EventId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter.Should().NotBeNull();
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task DuplicateProcessedEvent_DoesNotConfirmAgain()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_dup");
        var payload = CapturePayload("evt_dup", ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        (await PostWebhookAsync(payload.Body, payload.EventId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await PostWebhookAsync(payload.Body, payload.EventId);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await again.Content.ReadAsStringAsync();
        json.Should().Contain("duplicate");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Payments.CountAsync(p => p.BookingId == ctx.BookingId)).Should().Be(1);
        (await db.PaymentWebhookEvents.CountAsync(e => e.ProviderEventId == payload.EventId)).Should().Be(1);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task CrashResume_NullProcessedAt_Continues()
    {
        var ctx = await SeedAwaitingUpiAsync("wh_cr");
        const string eventId = "evt_cr";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PaymentWebhookEvents.Add(PaymentWebhookEvent.Create(
                PaymentWebhookOutcomes.ProviderRazorpay, eventId, true, "abc"));
            await db.SaveChangesAsync();
        }

        var payload = CapturePayload(eventId, ctx.PayId, ctx.OrderId, ctx.BookingId, ctx.AmountMinor);
        var response = await PostWebhookAsync(payload.Body, eventId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadOutcomeAsync(response)).Should().Be("captured");

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Bookings.SingleAsync(b => b.Id == ctx.BookingId)).Status.Should().Be(BookingStatus.Confirmed);
        (await verifyDb.PaymentWebhookEvents.CountAsync(e => e.ProviderEventId == eventId)).Should().Be(1);
        var row = await verifyDb.PaymentWebhookEvents.SingleAsync(e => e.ProviderEventId == eventId);
        row.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public void FullApiFactory_UpiRemainsDisabled_AndUsesFakes()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUpiCheckoutService>()
            .Should().BeSameAs(_factory.UpiCheckoutService);
        scope.ServiceProvider.GetRequiredService<IPaymentService>()
            .Should().BeOfType<DeterministicPaymentService>();
        _factory.UpiCheckoutService.Should().BeOfType<FakeUpiCheckoutService>();
    }

    private async Task<SeededUpi> SeedAwaitingUpiAsync(string prefix, string status = "captured")
    {
        _factory.UpiCheckoutService.ResetWebhookState();
        var (_, guest, spaceId) = await SeedVendorAndGuestListingAsync(prefix);
        _client.UseBearer(guest.AccessToken);
        var start = DateTime.UtcNow.AddMinutes(30);
        var (createResp, created) = await CreateBookingAsync(spaceId, start, start.AddHours(2));
        createResp.EnsureSuccessStatusCode();
        var bookingId = created!.Data!.Id;
        var amount = created.Data.TotalAmount;
        var amountMinor = ToMinor(amount);
        var orderId = "order_" + Guid.NewGuid().ToString("N")[..12];
        var payId = "pay_" + Guid.NewGuid().ToString("N")[..12];
        await SeedPaymentOrderAsync(bookingId, "booking", orderId, payId, amount);
        ConfigureSnapshot(payId, orderId, bookingId, amountMinor, amount, "booking", status);
        return new SeededUpi(bookingId, amount, amountMinor, orderId, payId);
    }

    private void ConfigureSnapshot(
        string payId,
        string orderId,
        Guid bookingId,
        long amountMinor,
        decimal amount,
        string purpose,
        string status = "captured")
    {
        var notes = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = purpose
        };
        _factory.UpiCheckoutService.Payments[payId] = new UpiPaymentSnapshot(
            payId, status, amountMinor, "INR", orderId, notes);
        _factory.UpiCheckoutService.Payments[orderId] = _factory.UpiCheckoutService.Payments[payId];
    }

    private async Task SeedPaymentOrderAsync(
        Guid bookingId,
        string purpose,
        string orderId,
        string payId,
        decimal amount,
        PaymentOrderStatus status = PaymentOrderStatus.Pending)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = PaymentOrder.Create(
            bookingId,
            purpose,
            PaymentOrder.RailUpi,
            PaymentOrder.ProviderRazorpay,
            orderId,
            amount,
            "INR",
            status,
            payId);
        db.PaymentOrders.Add(order);
        await db.SaveChangesAsync();
    }

    private async Task MutateOrderAsync(string orderId, Action<PaymentOrder> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await db.PaymentOrders.SingleAsync(o => o.ProviderOrderId == orderId);
        mutate(order);
        await db.SaveChangesAsync();
    }

    private async Task MutateBookingAsync(Guid bookingId, Action<Booking> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        mutate(booking);
        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(byte[] body, string eventId, byte[]? signBody = null)
    {
        return await SendRawAsync(body, Sign(signBody ?? body), eventId);
    }

    private async Task<HttpResponseMessage> SendRawAsync(byte[] body, string signature, string eventId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/razorpay")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add(RazorpayWebhookSignature.SignatureHeaderName, signature);
        request.Headers.Add(RazorpayWebhookSignature.EventIdHeaderName, eventId);
        return await _client.SendAsync(request);
    }

    private static (byte[] Body, string EventId) CapturePayload(
        string eventId,
        string payId,
        string orderId,
        Guid bookingId,
        long amountMinor,
        string eventType = "payment.captured",
        string purpose = "booking")
    {
        var json =
            "{\"event\":\"" + eventType + "\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + payId +
            "\",\"amount\":" + amountMinor + ",\"currency\":\"INR\",\"status\":\"captured\",\"order_id\":\"" +
            orderId + "\",\"notes\":{\"bookingId\":\"" + bookingId + "\",\"purpose\":\"" + purpose +
            "\"}}},\"order\":{\"entity\":{\"id\":\"" + orderId + "\"}}}}";
        return (Encoding.UTF8.GetBytes(json), eventId);
    }

    private static string Sign(byte[] body) =>
        RazorpayWebhookSignature.ComputeHex(body, FullApiFactory.UpiWebhookSecret);

    private static long ToMinor(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static async Task<string?> ReadOutcomeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("outcome", out var o) ? o.GetString() : null;
    }

    private async Task<(TokenDto Vendor, TokenDto Guest, Guid SpaceId)>
        SeedVendorAndGuestListingAsync(string prefix)
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
            description = "Webhook integration listing.",
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
            VehicleNumber: "KA01IT1234",
            VehicleModel: "Test",
            VehicleColor: "Blue",
            DiscountCode: null);

        var response = await _client.PostAsJsonAsync("/api/bookings", dto, HttpApiClientExtensions.JsonOptions);
        var body = await response.ReadApiResponseAsync<BookingDto>();
        return (response, body);
    }

    private sealed record SeededUpi(Guid BookingId, decimal Amount, long AmountMinor, string OrderId, string PayId);
}
