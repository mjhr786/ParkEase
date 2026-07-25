using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ParkingApp.Notifications.Infrastructure.Services;
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
    public void Constructor_MissingApiKey_DoesNotThrow()
    {
        // Implementation fail-opens: missing key means SendEmailAsync is a no-op.
        var httpClient = new HttpClient();
        var config = CreateConfiguration(null, "test@test.com");

        var act = () => new ResendEmailService(httpClient, config);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SendEmailAsync_Success_DoesNotThrow()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        var config = CreateConfiguration("re_test_key", "from@test.com");
        var service = new ResendEmailService(httpClient, config);

        var act = async () => await service.SendEmailAsync("to@test.com", "Subject", "<p>Body</p>");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendEmailAsync_MissingApiKey_DoesNotThrow()
    {
        var httpClient = new HttpClient();
        var config = CreateConfiguration(null, "from@test.com");
        var service = new ResendEmailService(httpClient, config);

        var act = async () => await service.SendEmailAsync("to@test.com", "Subject", "Body", isHtml: false);

        await act.Should().NotThrowAsync();
    }
}
