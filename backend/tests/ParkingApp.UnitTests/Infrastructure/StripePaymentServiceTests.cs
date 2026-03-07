using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Services;
using Stripe;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class StripePaymentServiceTests
{
    private readonly Mock<ILogger<StripePaymentService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly StripePaymentService _service;

    public StripePaymentServiceTests()
    {
        _loggerMock = new Mock<ILogger<StripePaymentService>>();
        
        var inMemorySettings = new Dictionary<string, string?> {
            {"Stripe:SecretKey", "sk_test_invalid_key_for_testing"}
        };
        
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _service = new StripePaymentService(_configuration, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_InvalidKey_ThrowsStripeException()
    {
        // Act
        var exception = await Record.ExceptionAsync(() => _service.CreateOrderAsync(100m, "USD", null, CancellationToken.None));

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<StripeException>();
    }

    [Fact]
    public async Task VerifyPaymentSignatureAsync_InvalidKey_ReturnsFalse()
    {
        // Act
        var result = await _service.VerifyPaymentSignatureAsync("pi_test", "order_test", "sig_test", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentAsync_InvalidKey_ReturnsFailedResult()
    {
        // Arrange
        var request = new PaymentRequest
        {
            BookingId = System.Guid.NewGuid(),
            UserId = System.Guid.NewGuid(),
            Amount = 50.0m,
            Currency = "USD",
            PaymentMethod = ParkingApp.Domain.Enums.PaymentMethod.CreditCard,
            Description = "Test"
        };

        // Act
        var result = await _service.ProcessPaymentAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessRefundAsync_InvalidKey_ReturnsFailedResult()
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPaymentStatusAsync_InvalidKey_ReturnsFailedStatus()
    {
        // Act
        var result = await _service.GetPaymentStatusAsync("pi_test", CancellationToken.None);

        // Assert
        result.Should().Be(PaymentStatus.Failed);
    }
}
