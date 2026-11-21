using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.DTOs;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.IntegrationTests.Http;

[Collection(FullApiHttpCollection.Name)]
public sealed class UpiPaymentsHttpTests : IDisposable
{
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;

    public UpiPaymentsHttpTests(FullApiPostgresFixture postgres)
    {
        _factory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task UpiCreateOrder_WhenFlagOff_Returns400UpiDisabled()
    {
        var tokens = await _client.RegisterAndGetTokensAsync("upi_flag_off");
        _client.UseBearer(tokens.AccessToken);

        var response = await _client.PostAsJsonAsync(
            "/api/payments/upi/create-order",
            new UpiCreateOrderDto(Guid.NewGuid()),
            HttpApiClientExtensions.JsonOptions);
        var body = await response.ReadApiResponseAsync<UpiCheckoutDto>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("upi_disabled");
        _factory.UpiCheckoutService.CallCount.Should().Be(0);
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public async Task Config_Anonymous_OmitsUpiContactRequired()
    {
        _client.ClearBearer();
        var response = await _client.GetAsync("/api/payments/config");
        var body = await response.ReadApiResponseAsync<PaymentsConfigDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.UpiEnabled.Should().BeFalse();
        body.Data.UpiContactRequired.Should().BeNull();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public void FullApiFactory_StillUsesDeterministicPaymentService()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPaymentService>()
            .Should().BeSameAs(_factory.PaymentService);
        scope.ServiceProvider.GetRequiredService<IStripeCheckoutService>()
            .Should().BeSameAs(_factory.PaymentService);
        scope.ServiceProvider.GetRequiredService<IUpiCheckoutService>()
            .Should().BeSameAs(_factory.UpiCheckoutService);
        _factory.PaymentService.Should().BeOfType<DeterministicPaymentService>();
    }

    [Fact]
    [Trait("Layer", "Http")]
    [Trait("Feature", "Payments")]
    public void FakeUpiCheckoutService_ReturnsUnmodifiedUpiPayIntent()
    {
        var session = _factory.UpiCheckoutService.CreateQrSessionAsync(
            new UpiQrSessionRequest(
                100m, "INR", "booking:test", "a@b.com", "9876543210",
                "203.0.113.10", "https://pe.fyro.cloud/", "ParkEase/web"))
            .GetAwaiter().GetResult();

        session.UpiIntentUrl.Should().StartWith("upi://pay?");
        session.UpiIntentUrl.Should().NotContain("rzp.io");
    }
}
