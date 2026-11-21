using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class PaymentWebhooksControllerTests
{
    private const string Secret = "controller_hmac_secret";
    private readonly Mock<IPaymentWebhookProcessor> _processor = new();
    private readonly Mock<IStripeWebhookProcessor> _stripe = new();
    private readonly PaymentWebhooksController _controller;

    public PaymentWebhooksControllerTests()
    {
        _processor
            .Setup(p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentWebhookProcessResult(200, new { outcome = "ignored_unhandled" }));
        _stripe
            .Setup(p => p.ProcessAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentWebhookProcessResult(200, new { outcome = "ignored_unhandled" }));

        _controller = new PaymentWebhooksController(
            _processor.Object,
            Options.Create(new UpiPaymentsOptions { Enabled = false, WebhookSecret = Secret }),
            NullLogger<PaymentWebhooksController>.Instance,
            _stripe.Object);
    }

    [Fact]
    public async Task Post_UnknownProvider_Returns404()
    {
        SetRequest("{}"u8.ToArray(), "application/json", Sign("{}"u8.ToArray()));

        var result = await _controller.Post("paypal", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_NonJsonContentType_Returns415()
    {
        SetRequest("{}"u8.ToArray(), "text/plain", Sign("{}"u8.ToArray()));

        var result = await _controller.Post("razorpay", CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(415);
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_EmptyBody_Returns400()
    {
        SetRequest(Array.Empty<byte>(), "application/json", "ab");

        var result = await _controller.Post("razorpay", CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Post_MissingSignature_Returns400()
    {
        SetRequest("{}"u8.ToArray(), "application/json", signature: null);

        var result = await _controller.Post("razorpay", CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("gg")]
    [InlineData("")]
    public async Task Post_MalformedSignature_Returns400(string header)
    {
        SetRequest("{}"u8.ToArray(), "application/json", header);

        var result = await _controller.Post("razorpay", CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(400);
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_WrongSecret_Returns400()
    {
        var body = """{"event":"qr_code.credited"}"""u8.ToArray();
        var wrong = RazorpayWebhookSignature.ComputeHex(body, "other_secret");
        SetRequest(body, "application/json", wrong);

        var result = await _controller.Post("razorpay", CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(400);
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_ValidSignature_PassesRawBytesToProcessor()
    {
        var json = """{ "event" : "qr_code.credited", "payload": { "payment": { "entity": { "id": "pay_raw" } } } }""";
        var body = Encoding.UTF8.GetBytes(json);
        SetRequest(body, "application/json", Sign(body), "evt_raw");

        var result = await _controller.Post("razorpay", CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(200);
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.Is<byte[]>(b => b.SequenceEqual(body)),
                It.IsAny<string>(),
                "evt_raw",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_LockNotAcquired_Returns503RetryAfter()
    {
        var body = "{}"u8.ToArray();
        _processor
            .Setup(p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentWebhookProcessResult(503, new { error = "lock_not_acquired" }, "15"));
        SetRequest(body, "application/json", Sign(body));

        var result = await _controller.Post("razorpay", CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(503);
        _controller.Response.Headers.RetryAfter.ToString().Should().Be("15");
    }

    [Fact]
    public async Task Post_Stripe_PassesBufferedBodyToStripeProcessor()
    {
        var body = """{"type":"payment_intent.succeeded"}"""u8.ToArray();
        SetRequest(body, "application/json", signature: null);
        _controller.HttpContext.Request.Headers["Stripe-Signature"] = "t=1,v1=abc";

        var result = await _controller.Post("stripe", CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(200);
        _stripe.Verify(
            p => p.ProcessAsync(
                It.Is<byte[]>(b => b.SequenceEqual(body)),
                "t=1,v1=abc",
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _processor.Verify(
            p => p.ProcessRazorpayAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static string Sign(byte[] body) => RazorpayWebhookSignature.ComputeHex(body, Secret);

    private void SetRequest(byte[] body, string contentType, string? signature, string? eventId = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.ContentType = contentType;
        http.Request.Body = new MemoryStream(body);
        http.Request.ContentLength = body.Length;
        if (signature is not null)
            http.Request.Headers[RazorpayWebhookSignature.SignatureHeaderName] = signature;
        if (eventId is not null)
            http.Request.Headers[RazorpayWebhookSignature.EventIdHeaderName] = eventId;
        http.Response.Body = new MemoryStream();
        _controller.ControllerContext = new ControllerContext { HttpContext = http };
    }
}
