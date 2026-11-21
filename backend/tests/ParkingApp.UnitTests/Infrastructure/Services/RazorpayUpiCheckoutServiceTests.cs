using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Infrastructure.Services;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class RazorpayUpiCheckoutServiceTests
{
    [Fact]
    public async Task CreateQrSessionAsync_PostsCreateJsonNotCreateUpi_AndReadsIntentUrl()
    {
        var handler = new RecordingHandler
        {
            Responder = request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/v1/orders", StringComparison.Ordinal))
                {
                    return Json(HttpStatusCode.OK, """{"id":"order_abc123"}""");
                }

                path.Should().Contain("/v1/payments/create/json");
                path.Should().NotContain("/create/upi");
                return Json(HttpStatusCode.OK, """
                    {
                      "razorpay_payment_id": "pay_abc123",
                      "next": [
                        { "action": "poll", "url": "https://api.razorpay.com/v1/payments/pay_abc123/status" },
                        { "action": "intent", "url": "upi://pay?pa=merchant@upi&pn=ParkEase&am=150.00" }
                      ]
                    }
                    """);
            }
        };

        var service = CreateService(handler);
        var session = await service.CreateQrSessionAsync(SampleRequest());

        session.ProviderOrderId.Should().Be("order_abc123");
        session.ProviderPaymentId.Should().Be("pay_abc123");
        session.UpiIntentUrl.Should().Be("upi://pay?pa=merchant@upi&pn=ParkEase&am=150.00");
        session.DisplayVpa.Should().Be("merchant@upi");

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/orders");
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/v1/payments/create/json");
        handler.Requests.Should().OnlyContain(r => string.IsNullOrEmpty(r.RequestUri!.Query));
        handler.Requests.Should().OnlyContain(r => r.Headers.Authorization!.Scheme == "Basic");

        using var createJson = JsonDocument.Parse(handler.Bodies[1]);
        createJson.RootElement.GetProperty("amount").GetInt64().Should().Be(15000);
        createJson.RootElement.GetProperty("currency").GetString().Should().Be("INR");
        createJson.RootElement.GetProperty("order_id").GetString().Should().Be("order_abc123");
        createJson.RootElement.GetProperty("method").GetString().Should().Be("upi");
    }

    [Fact]
    public async Task CreateQrSessionAsync_ImageUrlOnly_Fails()
    {
        var handler = new RecordingHandler
        {
            Responder = request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/v1/orders", StringComparison.Ordinal))
                    return Json(HttpStatusCode.OK, """{"id":"order_qr"}""");
                return Json(HttpStatusCode.OK, """
                    {
                      "razorpay_payment_id": "pay_qr",
                      "next": [
                        { "action": "image_url", "url": "https://rzp.io/i/abc" }
                      ]
                    }
                    """);
            }
        };

        var service = CreateService(handler);
        var act = () => service.CreateQrSessionAsync(SampleRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*intent*");
    }

    [Fact]
    public async Task GetPaymentAsync_GetsPaymentAndMapsNotes()
    {
        var handler = new RecordingHandler
        {
            Responder = request =>
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri!.AbsolutePath.Should().Be("/v1/payments/pay_abc");
                return Json(HttpStatusCode.OK, """
                    {
                      "id": "pay_abc",
                      "status": "captured",
                      "amount": 15000,
                      "currency": "INR",
                      "order_id": "order_abc",
                      "notes": { "bookingId": "11111111-1111-1111-1111-111111111111", "purpose": "booking" }
                    }
                    """);
            }
        };

        var snapshot = await CreateService(handler).GetPaymentAsync("pay_abc");

        snapshot.Id.Should().Be("pay_abc");
        snapshot.Status.Should().Be("captured");
        snapshot.AmountMinor.Should().Be(15000);
        snapshot.OrderId.Should().Be("order_abc");
        snapshot.Notes["purpose"].Should().Be("booking");
    }

    [Fact]
    public async Task GetPaymentAsync_OrderId_UsesOrderPaymentsCollection()
    {
        var handler = new RecordingHandler
        {
            Responder = request =>
            {
                request.RequestUri!.AbsolutePath.Should().Be("/v1/orders/order_abc/payments");
                return Json(HttpStatusCode.OK, """
                    {
                      "items": [
                        { "id": "pay_auth", "status": "authorized", "amount": 15000, "currency": "INR", "order_id": "order_abc" },
                        { "id": "pay_cap", "status": "captured", "amount": 15000, "currency": "INR", "order_id": "order_abc" }
                      ]
                    }
                    """);
            }
        };

        var snapshot = await CreateService(handler).GetPaymentAsync("order_abc");
        snapshot.Id.Should().Be("pay_cap");
        snapshot.Status.Should().Be("captured");
    }

    [Fact]
    public async Task RefundAsync_PostsRefundEndpoint()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => Json(HttpStatusCode.OK, """{"id":"rfnd_1","status":"processed"}""")
        };

        var refund = await CreateService(handler).RefundAsync("pay_abc", 150m, "INR");
        refund.Success.Should().BeTrue();
        refund.RefundId.Should().Be("rfnd_1");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/payments/pay_abc/refund");
        handler.Bodies[0].Should().Contain("\"amount\":15000");
    }

    [Fact]
    public void FakeUpiCheckoutService_ReturnsUpiPayUri_NotHostedLink()
    {
        var fake = new InlineFakeUpiCheckoutService();
        var session = fake.CreateQrSessionAsync(SampleRequest()).GetAwaiter().GetResult();

        session.UpiIntentUrl.Should().StartWith("upi://pay?");
        session.UpiIntentUrl.Should().NotContain("rzp.io");
        session.UpiIntentUrl.Should().NotStartWith("https://");
    }

    private static RazorpayUpiCheckoutService CreateService(RecordingHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.razorpay.com/")
        };
        return new RazorpayUpiCheckoutService(
            new StubFactory(client),
            Options.Create(new UpiPaymentsOptions
            {
                Enabled = true,
                KeyId = "rzp_test_key",
                KeySecret = "rzp_test_secret"
            }),
            NullLogger<RazorpayUpiCheckoutService>.Instance);
    }

    private static UpiQrSessionRequest SampleRequest() =>
        new(150m, "INR", "booking:abc", "ada@example.com", "9876543210",
            "203.0.113.10", "https://pe.fyro.cloud/", "ParkEase/web");

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responder(request);
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class InlineFakeUpiCheckoutService : IUpiCheckoutService
    {
        public Task<UpiQrSession> CreateQrSessionAsync(
            UpiQrSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            var intent = $"upi://pay?pa=parkease@razorpay&pn=ParkEase&am={request.Amount}&cu=INR";
            return Task.FromResult(new UpiQrSession("order_test", "pay_test", intent, "parkease@razorpay"));
        }

        public Task<UpiPaymentSnapshot> GetPaymentAsync(
            string providerPaymentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpiPaymentSnapshot(
                providerPaymentId, "captured", 15000, "INR", "order_test",
                new Dictionary<string, string>()));

        public Task<UpiRefundResult> RefundAsync(
            string providerPaymentId,
            decimal amount,
            string currency,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UpiRefundResult(true, "rfnd_test", "processed", null));

        public bool MatchesExpected(UpiPaymentSnapshot snapshot, PaymentCaptureCheck expected) => true;
    }
}
