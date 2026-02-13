using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Stripe;

namespace ParkingApp.Infrastructure.Services;

public class StripePaymentService : IPaymentService
{
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IConfiguration configuration, ILogger<StripePaymentService> logger)
    {
        _logger = logger;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateOrderAsync(decimal amount, string currency = "INR", Dictionary<string, string>? notes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to smallest currency unit (paisa)
                Currency = currency.ToLower(),
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = notes ?? new Dictionary<string, string>()
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, cancellationToken: cancellationToken);

            _logger.LogInformation("Created Stripe PaymentIntent {PaymentIntentId} for amount {Amount} {Currency}",
                paymentIntent.Id, amount, currency);

            // Return the client secret (frontend needs this to confirm payment)
            return paymentIntent.ClientSecret;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to create Stripe PaymentIntent");
            throw;
        }
    }

    public async Task<bool> VerifyPaymentSignatureAsync(string paymentIntentId, string orderId, string signature, CancellationToken cancellationToken = default)
    {
        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            var isSucceeded = paymentIntent.Status == "succeeded";
            _logger.LogInformation("Stripe PaymentIntent {PaymentIntentId} verification: Status={Status}, Succeeded={Succeeded}",
                paymentIntentId, paymentIntent.Status, isSucceeded);

            return isSucceeded;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to verify Stripe payment {PaymentIntentId}", paymentIntentId);
            return false;
        }
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a PaymentIntent for server-side processing
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(request.Amount * 100),
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
    }

    public async Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = request.PaymentId.ToString(),
                Amount = (long)(request.Amount * 100),
                Reason = "requested_by_customer"
            };

            var service = new RefundService();
            var refund = await service.CreateAsync(options, cancellationToken: cancellationToken);

            return new RefundResult
            {
                Success = refund.Status == "succeeded",
                RefundTransactionId = refund.Id,
                RefundedAmount = request.Amount
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for payment {PaymentId}", request.PaymentId);
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
