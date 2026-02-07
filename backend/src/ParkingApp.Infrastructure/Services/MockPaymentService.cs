using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// Mock payment service for development. Replace with actual payment gateway integration.
/// </summary>
public class MockPaymentService : IPaymentService
{
    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Simulate payment processing delay
        await Task.Delay(500, cancellationToken);

        // In a real implementation, this would call a payment gateway (Stripe, PayPal, etc.)
        // For now, we simulate successful payments
        
        var transactionId = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        
        // Simulate 95% success rate
        var random = new Random();
        var isSuccess = random.Next(100) < 95;

        if (isSuccess)
        {
            return new PaymentResult
            {
                Success = true,
                TransactionId = transactionId,
                PaymentGatewayReference = $"PG-{Guid.NewGuid().ToString()[..12].ToUpper()}",
                Status = PaymentStatus.Completed,
                ReceiptUrl = $"https://receipts.parkingapp.local/{transactionId}"
            };
        }
        else
        {
            return new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                ErrorMessage = "Payment declined by the bank. Please try again or use a different payment method."
            };
        }
    }

    public async Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        // Simulate refund processing delay
        await Task.Delay(300, cancellationToken);

        var refundTransactionId = $"RFD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        return new RefundResult
        {
            Success = true,
            RefundTransactionId = refundTransactionId,
            RefundedAmount = request.Amount
        };
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        
        // In a real implementation, this would query the payment gateway
        return PaymentStatus.Completed;
    }
}
