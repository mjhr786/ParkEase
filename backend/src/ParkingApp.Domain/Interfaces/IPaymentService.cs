using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Interfaces;

public class PaymentRequest
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentMethod PaymentMethod { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentGatewayReference { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ReceiptUrl { get; set; }
}

public class RefundRequest
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RefundResult
{
    public bool Success { get; set; }
    public string? RefundTransactionId { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
    Task<RefundResult> ProcessRefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
    Task<PaymentStatus> GetPaymentStatusAsync(string transactionId, CancellationToken cancellationToken = default);
}
