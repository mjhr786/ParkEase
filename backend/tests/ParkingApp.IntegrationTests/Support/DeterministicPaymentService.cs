using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// Deterministic payment gateway for integration tests (no network, no randomness).
/// Used by L2 payment lifecycle IT and L4 <see cref="FullApiFactory"/> HTTP journeys.
/// </summary>
public sealed class DeterministicPaymentService : IPaymentService, IStripeCheckoutService
{
    public bool VerifySignatureResult { get; set; } = true;
    public bool RefundSucceeds { get; set; } = true;
    public bool ProcessPaymentSucceeds { get; set; } = true;

    public string? LastOrderId { get; private set; }
    public decimal? LastOrderAmount { get; private set; }
    public Dictionary<string, string>? LastOrderNotes { get; private set; }
    public string? LastVerifiedPaymentId { get; private set; }
    public PaymentCaptureCheck? LastExpectedCapture { get; private set; }
    public string? LastRefundedGatewayTxnId { get; private set; }

    public Task<CardCheckoutSession> CreateCardSessionAsync(
        decimal amount,
        string currency = "INR",
        Dictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default)
    {
        LastOrderAmount = amount;
        LastOrderNotes = notes is null ? null : new Dictionary<string, string>(notes);
        LastOrderId = $"pi_test_{Guid.NewGuid():N}"[..24];
        return Task.FromResult(new CardCheckoutSession(LastOrderId, LastOrderId));
    }

    public async Task<string> CreateOrderAsync(
        decimal amount,
        string currency = "INR",
        Dictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default)
    {
        var session = await CreateCardSessionAsync(amount, currency, notes, cancellationToken);
        return session.ClientSecret;
    }

    public Task<bool> VerifyPaymentSignatureAsync(
        string paymentId,
        string orderId,
        string signature,
        CancellationToken cancellationToken = default,
        PaymentCaptureCheck? expected = null)
    {
        LastVerifiedPaymentId = paymentId;
        LastExpectedCapture = expected;
        return Task.FromResult(VerifySignatureResult);
    }

    public Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ProcessPaymentSucceeds)
        {
            return Task.FromResult(new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                ErrorMessage = "Card declined (test)"
            });
        }

        var txn = $"pi_proc_{Guid.NewGuid():N}"[..20];
        return Task.FromResult(new PaymentResult
        {
            Success = true,
            TransactionId = txn,
            PaymentGatewayReference = $"ch_{Guid.NewGuid():N}"[..16],
            Status = PaymentStatus.Completed,
            ReceiptUrl = $"https://receipts.test/{txn}"
        });
    }

    public Task<RefundResult> ProcessRefundAsync(
        RefundRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRefundedGatewayTxnId = request.GatewayTransactionId;
        if (!RefundSucceeds)
        {
            return Task.FromResult(new RefundResult
            {
                Success = false,
                RefundedAmount = 0,
                ErrorMessage = "Refund declined (test)"
            });
        }

        return Task.FromResult(new RefundResult
        {
            Success = true,
            RefundTransactionId = $"re_test_{Guid.NewGuid():N}"[..18],
            RefundedAmount = request.Amount
        });
    }

    public Task<PaymentStatus> GetPaymentStatusAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentStatus.Completed);
}
