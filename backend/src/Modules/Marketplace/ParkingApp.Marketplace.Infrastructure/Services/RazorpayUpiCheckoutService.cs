using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal sealed class RazorpayUpiCheckoutService : IUpiCheckoutService
{
    public const string HttpClientName = "RazorpayUpi";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<UpiPaymentsOptions> _options;
    private readonly ILogger<RazorpayUpiCheckoutService> _logger;

    public RazorpayUpiCheckoutService(
        IHttpClientFactory httpClientFactory,
        IOptions<UpiPaymentsOptions> options,
        ILogger<RazorpayUpiCheckoutService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<UpiQrSession> CreateQrSessionAsync(
        UpiQrSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var keyId = _options.Value.KeyId?.Trim() ?? string.Empty;
        var keySecret = _options.Value.KeySecret?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
            throw new InvalidOperationException("UPI provider is not configured");

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        var amount = PaymentCaptureMatcher.ToMinorUnits(request.Amount);
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "INR" : request.Currency;

        var orderPayload = new
        {
            amount,
            currency,
            receipt = Truncate(request.Receipt, 40),
            payment_capture = 1,
            notes = request.Notes
        };

        using var orderDoc = await PostJsonAsync(client, authValue, "v1/orders", orderPayload, cancellationToken);
        var orderId = orderDoc.RootElement.TryGetProperty("id", out var orderIdEl)
            ? orderIdEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(orderId))
            throw new InvalidOperationException("Razorpay order response did not include id");

        var paymentPayload = new
        {
            amount,
            currency,
            order_id = orderId,
            method = "upi",
            upi = new { flow = "intent" },
            email = request.Email,
            contact = request.Contact,
            ip = request.ClientIp,
            referer = request.Referer,
            user_agent = request.UserAgent,
            notes = request.Notes
        };

        using var paymentDoc = await PostJsonAsync(
            client, authValue, "v1/payments/create/json", paymentPayload, cancellationToken);

        var root = paymentDoc.RootElement;
        var paymentId = root.TryGetProperty("razorpay_payment_id", out var payEl)
            ? payEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(paymentId))
            throw new InvalidOperationException("Razorpay create/json response did not include razorpay_payment_id");

        if (!TryReadIntentUrl(root, out var intentUrl))
            throw new InvalidOperationException("Razorpay create/json response did not include next[].action=intent");

        _logger.LogInformation(
            "UPI intent created OrderId={OrderId} PaymentId={PaymentId} Intent={Intent}",
            orderId,
            paymentId,
            PaymentObservability.RedactIntentUrl(intentUrl));

        return new UpiQrSession(orderId, paymentId, intentUrl, TryReadVpa(intentUrl));
    }

    public async Task<UpiPaymentSnapshot> GetPaymentAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
            throw new InvalidOperationException("Provider payment id is required");

        var client = CreateAuthorizedClient(out var authValue);
        if (providerPaymentId.StartsWith("order_", StringComparison.OrdinalIgnoreCase))
        {
            using var collection = await SendJsonAsync(
                client, authValue, HttpMethod.Get, $"v1/orders/{providerPaymentId}/payments", payload: null, cancellationToken);
            return MapCollectionSnapshot(collection.RootElement, providerPaymentId);
        }

        using var doc = await SendJsonAsync(
            client, authValue, HttpMethod.Get, $"v1/payments/{providerPaymentId}", payload: null, cancellationToken);
        return MapPaymentSnapshot(doc.RootElement);
    }

    public async Task<UpiRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
            throw new InvalidOperationException("Provider payment id is required");

        var client = CreateAuthorizedClient(out var authValue);
        var payload = new
        {
            amount = PaymentCaptureMatcher.ToMinorUnits(amount),
            currency = string.IsNullOrWhiteSpace(currency) ? "INR" : currency
        };

        using var doc = await SendJsonAsync(
            client,
            authValue,
            HttpMethod.Post,
            $"v1/payments/{providerPaymentId}/refund",
            payload,
            cancellationToken);

        var root = doc.RootElement;
        var refundId = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : null;
        var status = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("status", out var statusEl)
            ? statusEl.GetString()
            : null;

        if (!string.IsNullOrEmpty(status)
            && !status.Equals("processed", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("pending", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("created", StringComparison.OrdinalIgnoreCase))
        {
            return new UpiRefundResult(
                false,
                refundId,
                status,
                $"Razorpay refund status '{status}' is not successful");
        }

        return new UpiRefundResult(true, refundId, status ?? "processed", null);
    }

    public bool MatchesExpected(UpiPaymentSnapshot snapshot, PaymentCaptureCheck expected) =>
        PaymentCaptureMatcher.Matches(snapshot.AmountMinor, snapshot.Currency, snapshot.Notes, expected);

    internal static bool TryReadIntentUrl(JsonElement root, out string intentUrl)
    {
        intentUrl = string.Empty;
        if (!root.TryGetProperty("next", out var next) || next.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var step in next.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
                continue;
            if (!step.TryGetProperty("action", out var actionEl))
                continue;
            if (!string.Equals(actionEl.GetString(), "intent", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!step.TryGetProperty("url", out var urlEl))
                continue;
            var url = urlEl.GetString();
            if (string.IsNullOrWhiteSpace(url))
                continue;
            intentUrl = url;
            return true;
        }

        return false;
    }

    internal static string? TryReadVpa(string intentUrl)
    {
        if (!Uri.TryCreate(intentUrl, UriKind.Absolute, out var uri))
            return null;
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            if (!string.Equals(part[..eq], "pa", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private HttpClient CreateAuthorizedClient(out string authValue)
    {
        var keyId = _options.Value.KeyId?.Trim() ?? string.Empty;
        var keySecret = _options.Value.KeySecret?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
            throw new InvalidOperationException("UPI provider is not configured");

        authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        return _httpClientFactory.CreateClient(HttpClientName);
    }

    private async Task<JsonDocument> PostJsonAsync(
        HttpClient client,
        string authValue,
        string relativePath,
        object payload,
        CancellationToken cancellationToken) =>
        await SendJsonAsync(client, authValue, HttpMethod.Post, relativePath, payload, cancellationToken);

    private async Task<JsonDocument> SendJsonAsync(
        HttpClient client,
        string authValue,
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        if (payload is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Razorpay {Path} failed with {StatusCode}",
                relativePath,
                (int)response.StatusCode);
            throw new InvalidOperationException($"Razorpay {relativePath} failed with status {(int)response.StatusCode}");
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    internal static UpiPaymentSnapshot MapPaymentSnapshot(JsonElement root)
    {
        var id = ReadString(root, "id") ?? string.Empty;
        var status = ReadString(root, "status") ?? string.Empty;
        var currency = ReadString(root, "currency") ?? "INR";
        var orderId = ReadString(root, "order_id");
        var amountMinor = 0L;
        if (root.TryGetProperty("amount", out var amountEl) && amountEl.TryGetInt64(out var amount))
            amountMinor = amount;

        return new UpiPaymentSnapshot(id, status, amountMinor, currency, orderId, ReadNotes(root));
    }

    internal static UpiPaymentSnapshot MapCollectionSnapshot(JsonElement root, string fallbackOrderId)
    {
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            JsonElement? captured = null;
            JsonElement? first = null;
            foreach (var item in items.EnumerateArray())
            {
                first ??= item;
                if (item.TryGetProperty("status", out var statusEl)
                    && string.Equals(statusEl.GetString(), "captured", StringComparison.OrdinalIgnoreCase))
                {
                    captured = item;
                    break;
                }
            }

            var chosen = captured ?? first;
            if (chosen is JsonElement entity)
            {
                var snapshot = MapPaymentSnapshot(entity);
                return snapshot with { OrderId = snapshot.OrderId ?? fallbackOrderId };
            }
        }

        return new UpiPaymentSnapshot(
            fallbackOrderId,
            "unknown",
            0,
            "INR",
            fallbackOrderId,
            new Dictionary<string, string>());
    }

    private static IReadOnlyDictionary<string, string> ReadNotes(JsonElement root)
    {
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("notes", out var notesEl) || notesEl.ValueKind != JsonValueKind.Object)
            return notes;

        foreach (var prop in notesEl.EnumerateObject())
        {
            notes[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.GetRawText()
            };
        }

        return notes;
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) ? el.GetString() : null;

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
