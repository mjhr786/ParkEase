using FluentAssertions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class MockRazorpayPaymentServiceTests
{
    private readonly MockRazorpayPaymentService _service;

    public MockRazorpayPaymentServiceTests()
    {
        _service = new MockRazorpayPaymentService();
    }

    [Fact]
    public async Task CreateOrderAsync_ReturnsMockOrderId()
    {
        // Act
        var result = await _service.CreateOrderAsync(100.00m, "INR", null, CancellationToken.None);

        // Assert
        result.Should().StartWith("order_mock_");
    }

    [Fact]
    public async Task VerifyPaymentSignatureAsync_ValidSignature_ReturnsTrue()
    {
        // Act
        var result = await _service.VerifyPaymentSignatureAsync("pay_123", "order_123", "valid_sig", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPaymentSignatureAsync_InvalidSignature_ReturnsFalse()
    {
        // Act
        var result = await _service.VerifyPaymentSignatureAsync("pay_123", "order_123", "invalid_signature", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentAsync_ReturnsSuccessResult()
    {
        // Arrange
        var request = new PaymentRequest
        {
            BookingId = System.Guid.NewGuid(),
            UserId = System.Guid.NewGuid(),
            Amount = 50.0m,
            Currency = "INR",
            PaymentMethod = ParkingApp.Domain.Enums.PaymentMethod.CreditCard,
            Description = "Test Pay"
        };

        // Act
        var result = await _service.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TransactionId.Should().StartWith("pay_mock_");
        result.PaymentGatewayReference.Should().StartWith("ref_");
        result.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task ProcessRefundAsync_ReturnsSuccessfulRefund()
    {
        // Arrange
        var request = new RefundRequest
        {
            PaymentId = System.Guid.NewGuid(),
            Amount = 50.0m,
            Reason = "Cancel"
        };

        // Act
        var result = await _service.ProcessRefundAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RefundTransactionId.Should().StartWith("rfn_mock_");
        result.RefundedAmount.Should().Be(50.0m);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_ReturnsCompleted()
    {
        // Act
        var result = await _service.GetPaymentStatusAsync("pay_mock_123", CancellationToken.None);

        // Assert
        result.Should().Be(PaymentStatus.Completed);
    }
}
