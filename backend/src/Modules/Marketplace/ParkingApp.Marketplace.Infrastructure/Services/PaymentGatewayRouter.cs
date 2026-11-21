using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>
/// Production <see cref="IPaymentService"/>: Stripe for capture and for <c>pi_</c> refunds
/// (including historical rows stored as Razorpay with Stripe PaymentIntent ids).
/// <c>pay_</c> refunds go to Razorpay UPI. MockGateway fails closed.
/// </summary>
internal sealed class PaymentGatewayRouter : IPaymentService
{
    private readonly IPaymentService _stripe;
    private readonly IUpiCheckoutService? _upi;

    internal PaymentGatewayRouter(IPaymentService stripe, IUpiCheckoutService? upi = null)
    {
        _stripe = stripe;
        _upi = upi;
    }

    public PaymentGatewayRouter(StripePaymentService stripe, IUpiCheckoutService upi)
        : this((IPaymentService)stripe, upi)
    {
    }

    public Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
        => _stripe.ProcessPaymentAsync(request, cancellationToken);

    public Task<string> CreateOrderAsync(decimal amount, string currency = "INR", Dictionary<string, string>? notes = null, CancellationToken cancellationToken = default)
        => _stripe.CreateOrderAsync(amount, currency, notes, cancellationToken);

    public Task<bool> VerifyPaymentSignatureAsync(
        string paymentId,
        string orderId,
        string signature,
        CancellationToken cancellationToken = default,
        PaymentCaptureCheck? expected = null)
        => _stripe.VerifyPaymentSignatureAsync(paymentId, orderId, signature, cancellationToken, expected);

    public Task<PaymentStatus> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
        => _stripe.GetPaymentStatusAsync(transactionId, cancellationToken);

    public async Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        if (string.Equals(request.PaymentGateway, ProcessPaymentHandler.MockGateway, StringComparison.OrdinalIgnoreCase))
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "MockGateway refunds are not available"
            };
        }

        var gatewayTransactionId = request.GatewayTransactionId?.Trim();
        if (string.IsNullOrEmpty(gatewayTransactionId))
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "Missing gateway transaction id for refund"
            };
        }

        if (gatewayTransactionId.StartsWith("pi_", StringComparison.Ordinal))
            return await _stripe.ProcessRefundAsync(request, cancellationToken);

        if (gatewayTransactionId.StartsWith("pay_", StringComparison.Ordinal))
            return await RefundUpiAsync(request, gatewayTransactionId, cancellationToken);

        return new RefundResult
        {
            Success = false,
            ErrorMessage = "Unknown payment gateway; refunds are not available"
        };
    }

    private async Task<RefundResult> RefundUpiAsync(
        RefundRequest request,
        string gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        if (_upi is null)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = "UPI refunds are not available"
            };
        }

        try
        {
            var result = await _upi.RefundAsync(
                gatewayTransactionId,
                request.Amount,
                "INR",
                cancellationToken);
            if (!result.Success)
            {
                return new RefundResult
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "UPI refund failed"
                };
            }

            return new RefundResult
            {
                Success = true,
                RefundTransactionId = result.RefundId,
                RefundedAmount = request.Amount
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RefundResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
