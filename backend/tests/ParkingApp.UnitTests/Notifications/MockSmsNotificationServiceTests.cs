using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Notifications.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class MockSmsNotificationServiceTests
{
    private readonly Mock<ILogger<MockSmsNotificationService>> _loggerMock;
    private readonly MockSmsNotificationService _service;

    public MockSmsNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<MockSmsNotificationService>>();
        _service = new MockSmsNotificationService(_loggerMock.Object);
    }

    [Fact]
    public async Task SendAsync_ReturnsSuccess()
    {
        // Act
        var result = await _service.SendAsync("1234567890", "Test message", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MessageId.Should().StartWith("SMS-");
    }

    [Fact]
    public async Task SendBulkAsync_ReturnsListOfResults()
    {
        // Arrange
        var numbers = new[] { "1234567890", "0987654321" };

        // Act
        var results = await _service.SendBulkAsync(numbers, "Bulk message", CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        foreach (var result in results)
        {
            result.Success.Should().BeTrue();
            result.MessageId.Should().StartWith("SMS-");
        }
    }

    [Fact]
    public async Task SendTemplatedAsync_ReturnsSuccess()
    {
        // Arrange
        var placeholders = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Name", "John" },
            { "Code", "1234" }
        };

        // Act
        var result = await _service.SendTemplatedAsync("1234567890", SmsTemplates.OtpVerification, placeholders, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MessageId.Should().StartWith("SMS-");
    }
}
