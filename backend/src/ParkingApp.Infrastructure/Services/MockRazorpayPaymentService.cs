using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// Mock implementation of Razorpay payment service for development/testing.
/// Simulates the behavior of Razorpay API without actual calls.
/// </summary>
public class MockRazorpayPaymentService : IPaymentService
{
    // Simulate a secret for signature verification (in real detailed mock scenarios)
    private const string MockSecret = "mock_secret_123";

    public async Task<string> CreateOrderAsync(decimal amount, string currency = "INR", Dictionary<string, string>? notes = null, CancellationToken cancellationToken = default)
    {
        // Simulate network delay
        await Task.Delay(500, cancellationToken);

        // Generate a mock order ID following Razorpay format
        // e.g., order_Kz2w3x4y5z6a7b
        var orderId = $"order_mock_{Guid.NewGuid().ToString("N")[..14]}";
        
        return orderId;
    }

    public async Task<bool> VerifyPaymentSignatureAsync(string paymentId, string orderId, string signature, CancellationToken cancellationToken = default)
    {
        // Simulate verification delay
        await Task.Delay(300, cancellationToken);

        // In a real implementation, we would hash (orderId + "|" + paymentId) with the secret
        // For mock purposes, just return true to simulate successful verification
        // unless the signature is explicitly "invalid_signature"
        
        if (signature == "invalid_signature")
        {
            return false;
        }

        return true;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        // This method is for direct server-side payments (if needed in future)
        // For Razorpay standard flow, we use CreateOrder -> Client Checkout -> VerifySignature
        // But we keep this for compatibility if other parts of the system use it.
        
        await Task.Delay(500, cancellationToken);
        
        return new PaymentResult
        {
            Success = true,
            TransactionId = $"pay_mock_{Guid.NewGuid().ToString("N")[..14]}",
            PaymentGatewayReference = $"ref_{Guid.NewGuid().ToString("N")[..10]}",
            Status = PaymentStatus.Completed,
            ReceiptUrl = "https://mock-receipt-url.com"
        };
    }

    public async Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);
        
        return new RefundResult
        {
            Success = true,
            RefundTransactionId = $"rfn_mock_{Guid.NewGuid().ToString("N")[..14]}",
            RefundedAmount = request.Amount
        };
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken);
        return PaymentStatus.Completed;
    }
}
