using FluentAssertions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class MockPaymentServiceTests
{
    private readonly MockPaymentService _service;

    public MockPaymentServiceTests()
    {
        _service = new MockPaymentService();
    }

    [Fact]
    public async Task CreateOrderAsync_ReturnsMockOrderId()
    {
        // Act
        var result = await _service.CreateOrderAsync(100.00m, "USD", null, CancellationToken.None);

        // Assert
        result.Should().StartWith("order_mock_");
    }

    [Fact]
    public async Task VerifyPaymentSignatureAsync_ReturnsTrue()
    {
        // Act
        var result = await _service.VerifyPaymentSignatureAsync("pay_123", "order_123", "sig_123", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPaymentAsync_ReturnsPaymentResult()
    {
        // Arrange
        var request = new PaymentRequest
        {
            BookingId = System.Guid.NewGuid(),
            UserId = System.Guid.NewGuid(),
            Amount = 50.0m,
            Currency = "USD",
            PaymentMethod = ParkingApp.Domain.Enums.PaymentMethod.CreditCard,
            Description = "Test Pay"
        };

        // Act
        var result = await _service.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        if (result.Success)
        {
            result.TransactionId.Should().StartWith("TXN-");
            result.Status.Should().Be(PaymentStatus.Completed);
            result.ReceiptUrl.Should().NotBeNullOrEmpty();
        }
        else
        {
            result.Status.Should().Be(PaymentStatus.Failed);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
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
        result.RefundTransactionId.Should().StartWith("RFD-");
        result.RefundedAmount.Should().Be(50.0m);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_ReturnsCompleted()
    {
        // Act
        var result = await _service.GetPaymentStatusAsync("TXN-1234", CancellationToken.None);

        // Assert
        result.Should().Be(PaymentStatus.Completed);
    }
}
