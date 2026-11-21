using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;

namespace ParkingApp.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/webhooks")]
public sealed class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentWebhookProcessor _processor;
    private readonly IStripeWebhookProcessor _stripeProcessor;
    private readonly IOptions<UpiPaymentsOptions> _upiOptions;
    private readonly ILogger<PaymentWebhooksController> _logger;

    public PaymentWebhooksController(
        IPaymentWebhookProcessor processor,
        IOptions<UpiPaymentsOptions> upiOptions,
        ILogger<PaymentWebhooksController> logger,
        IStripeWebhookProcessor stripeProcessor)
    {
        _processor = processor;
        _stripeProcessor = stripeProcessor;
        _upiOptions = upiOptions;
        _logger = logger;
    }

    [HttpPost("{provider}")]
    [RequestSizeLimit(RazorpayWebhookSignature.MaxBodyBytes)]
    public async Task<IActionResult> Post(string provider, CancellationToken cancellationToken)
    {
        if (string.Equals(provider, "stripe", StringComparison.OrdinalIgnoreCase))
            return await PostStripe(cancellationToken);

        if (!string.Equals(provider, "razorpay", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (!RazorpayWebhookSignature.IsJsonContentType(Request.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (Request.ContentLength is > RazorpayWebhookSignature.MaxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        var raw = await ReadBodyAsync(Request.Body, RazorpayWebhookSignature.MaxBodyBytes, cancellationToken);
        if (raw is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (raw.Length == 0)
            return StatusCode(StatusCodes.Status400BadRequest);

        if (!Request.Headers.TryGetValue(RazorpayWebhookSignature.SignatureHeaderName, out var signatureValues)
            || !RazorpayWebhookSignature.TryNormalizeSignature(signatureValues.ToString(), out var headerHex))
        {
            return StatusCode(StatusCodes.Status400BadRequest);
        }

        var computed = RazorpayWebhookSignature.ComputeHex(raw, _upiOptions.Value.WebhookSecret ?? string.Empty);
        if (!RazorpayWebhookSignature.FixedTimeEqualsHex(computed, headerHex))
        {
            _logger.LogWarning("PaymentWebhookSignatureInvalid");
            return StatusCode(StatusCodes.Status400BadRequest);
        }

        Request.Headers.TryGetValue(RazorpayWebhookSignature.EventIdHeaderName, out var eventIdValues);
        var result = await _processor.ProcessRazorpayAsync(
            raw,
            RazorpayWebhookSignature.PayloadSha256Hex(raw),
            eventIdValues.ToString(),
            DateTime.UtcNow,
            cancellationToken);

        if (!string.IsNullOrEmpty(result.RetryAfter))
            Response.Headers.RetryAfter = result.RetryAfter;

        return StatusCode(result.StatusCode, result.Body);
    }

    private async Task<IActionResult> PostStripe(CancellationToken cancellationToken)
    {
        if (!RazorpayWebhookSignature.IsJsonContentType(Request.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (Request.ContentLength is > RazorpayWebhookSignature.MaxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        var raw = await ReadBodyAsync(Request.Body, RazorpayWebhookSignature.MaxBodyBytes, cancellationToken);
        if (raw is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (raw.Length == 0)
            return StatusCode(StatusCodes.Status400BadRequest);

        Request.Headers.TryGetValue("Stripe-Signature", out var signatureValues);
        var result = await _stripeProcessor.ProcessAsync(
            raw,
            signatureValues.ToString(),
            DateTime.UtcNow,
            cancellationToken);

        if (!string.IsNullOrEmpty(result.RetryAfter))
            Response.Headers.RetryAfter = result.RetryAfter;

        return StatusCode(result.StatusCode, result.Body);
    }

    internal static async Task<byte[]?> ReadBodyAsync(
        Stream body,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(Math.Min(maxBytes, 8192));
        var chunk = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read == 0)
                break;
            total += read;
            if (total > maxBytes)
                return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }
}
