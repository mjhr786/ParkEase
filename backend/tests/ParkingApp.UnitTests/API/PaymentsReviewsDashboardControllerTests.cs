using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ParkingApp.UnitTests.API;

public class PaymentsReviewsDashboardControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly PaymentsController _paymentsController;

    public PaymentsReviewsDashboardControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _mockConfiguration = new Mock<IConfiguration>();
        _paymentsController = new PaymentsController(_mockDispatcher.Object, _mockConfiguration.Object);
    }

    [Fact]
    public void GetStripeConfig_ShouldReturnPublishableKeyFromConfiguration()
    {
        // Arrange
        var expectedKey = "pk_test_123";
        _mockConfiguration.Setup(c => c["Stripe:PublishableKey"]).Returns(expectedKey);

        // Act
        var result = _paymentsController.GetStripeConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        // Use reflection or dynamic to check anonymous object property
        var value = okResult.Value;
        var publishableKeyProp = value?.GetType().GetProperty("publishableKey");
        publishableKeyProp.Should().NotBeNull();
        publishableKeyProp!.GetValue(value).Should().Be(expectedKey);
    }
}
