using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ParkingApp.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class ResendEmailServiceTests
{
    private IConfiguration CreateConfiguration(string? apiKey, string? fromEmail)
    {
        var settings = new Dictionary<string, string?>();
        if (apiKey != null) settings.Add("Resend:ApiKey", apiKey);
        if (fromEmail != null) settings.Add("Resend:FromEmail", fromEmail);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = CreateConfiguration(null, "test@test.com");

        // Act
        Action action = () => new ResendEmailService(httpClient, config);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("Resend:ApiKey is not configured.");
    }

    [Fact]
    public async Task SendEmailAsync_Success_DoesNotThrow()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfiguration("test_api_key", "test@test.com");
        var service = new ResendEmailService(httpClient, config);

        // Act
        var exception = await Record.ExceptionAsync(() => service.SendEmailAsync("to@test.com", "Subject", "Body", true));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task SendEmailAsync_Failure_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request Error Message")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfiguration("test_api_key", "test@test.com");
        var service = new ResendEmailService(httpClient, config);

        // Act
        var exception = await Record.ExceptionAsync(() => service.SendEmailAsync("to@test.com", "Subject", "Body", false));

        // Assert
        exception.Should().BeNull(); // Design choice: swallows exceptions and logs
    }

    [Fact]
    public async Task SendEmailAsync_ThrowsHttpException_SwallowsAndLogs()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network failure"));

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfiguration("test_api_key", "test@test.com");
        var service = new ResendEmailService(httpClient, config);

        // Act
        var exception = await Record.ExceptionAsync(() => service.SendEmailAsync("to@test.com", "Subject", "Body", false));

        // Assert
        exception.Should().BeNull(); // Swallowed
    }
}
