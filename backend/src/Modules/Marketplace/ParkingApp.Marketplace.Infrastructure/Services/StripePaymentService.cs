using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Marketplace.Contracts.Enums;
using Stripe;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal sealed class StripePaymentService : IPaymentService, IStripeCheckoutService
{
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IConfiguration configuration, ILogger<StripePaymentService> logger)
    {
        _logger = logger;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<CardCheckoutSession> CreateCardSessionAsync(
        decimal amount,
        string currency = "INR",
        Dictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            long stripeAmount = PaymentCaptureMatcher.ToMinorUnits(amount);

            var options = new PaymentIntentCreateOptions
            {
                Amount = stripeAmount,
                Currency = currency.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = notes ?? new Dictionary<string, string>()
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, cancellationToken: cancellationToken);

            _logger.LogInformation("Created Stripe PaymentIntent {PaymentIntentId} for amount {Amount} {Currency}",
                paymentIntent.Id, amount, currency);

            return new CardCheckoutSession(paymentIntent.Id, paymentIntent.ClientSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to create Stripe PaymentIntent");
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create Stripe PaymentIntent");
            throw new StripeException("Failed to create Stripe PaymentIntent", ex);
        }
    }

    public async Task<string> CreateOrderAsync(decimal amount, string currency = "INR", Dictionary<string, string>? notes = null, CancellationToken cancellationToken = default)
    {
        var session = await CreateCardSessionAsync(amount, currency, notes, cancellationToken);
        return session.ClientSecret;
    }

    public async Task<bool> VerifyPaymentSignatureAsync(
        string paymentIntentId,
        string orderId,
        string signature,
        CancellationToken cancellationToken = default,
        PaymentCaptureCheck? expected = null)
    {
        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            var isSucceeded = paymentIntent.Status == "succeeded";
            var matchesExpected = expected is not null
                && PaymentCaptureMatcher.Matches(
                    paymentIntent.Amount,
                    paymentIntent.Currency,
                    paymentIntent.Metadata,
                    expected);
            var isValid = isSucceeded && matchesExpected;
            _logger.LogInformation("Stripe PaymentIntent {PaymentIntentId} verification: Status={Status}, Succeeded={Succeeded}, CaptureMatch={CaptureMatch}",
                paymentIntentId, paymentIntent.Status, isSucceeded, matchesExpected);

            return isValid;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to verify Stripe payment {PaymentIntentId}", paymentIntentId);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to verify Stripe payment {PaymentIntentId}", paymentIntentId);
            return false;
        }
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            long stripeAmount = PaymentCaptureMatcher.ToMinorUnits(request.Amount);

            // Create a PaymentIntent for server-side processing
            var options = new PaymentIntentCreateOptions
            {
                Amount = stripeAmount,
                Currency = request.Currency.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                Description = request.Description,
                Metadata = new Dictionary<string, string>
                {
                    { "bookingId", request.BookingId.ToString() },
                    { "userId", request.UserId.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, cancellationToken: cancellationToken);

            return new PaymentResult
            {
                Success = paymentIntent.Status == "succeeded" || paymentIntent.Status == "requires_payment_method",
                TransactionId = paymentIntent.Id,
                PaymentGatewayReference = paymentIntent.ClientSecret,
                Status = MapStripeStatus(paymentIntent.Status),
                ReceiptUrl = null
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe payment processing failed for booking {BookingId}", request.BookingId);
            return new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stripe payment processing failed for booking {BookingId}", request.BookingId);
            return new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var paymentIntentId = request.GatewayTransactionId?.Trim();
            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                return new RefundResult
                {
                    Success = false,
                    ErrorMessage = "Missing gateway transaction id (Stripe PaymentIntent) for refund"
                };
            }

            var options = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Amount = PaymentCaptureMatcher.ToMinorUnits(request.Amount),
                Reason = "requested_by_customer"
            };

            var service = new RefundService();
            var refund = await service.CreateAsync(options, cancellationToken: cancellationToken);

            return new RefundResult
            {
                Success = refund.Status is "succeeded" or "pending",
                RefundTransactionId = refund.Id,
                RefundedAmount = request.Amount
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for payment {PaymentId} (PI={PaymentIntent})",
                request.PaymentId, request.GatewayTransactionId);
            return new RefundResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stripe refund failed for payment {PaymentId} (PI={PaymentIntent})",
                request.PaymentId, request.GatewayTransactionId);
            return new RefundResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(transactionId, cancellationToken: cancellationToken);
            return MapStripeStatus(paymentIntent.Status);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to get status for Stripe payment {TransactionId}", transactionId);
            return PaymentStatus.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get status for Stripe payment {TransactionId}", transactionId);
            return PaymentStatus.Failed;
        }
    }

    private static PaymentStatus MapStripeStatus(string stripeStatus) => stripeStatus switch
    {
        "succeeded" => PaymentStatus.Completed,
        "processing" => PaymentStatus.Pending,
        "requires_payment_method" => PaymentStatus.Pending,
        "requires_confirmation" => PaymentStatus.Pending,
        "requires_action" => PaymentStatus.Pending,
        "canceled" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };
}

