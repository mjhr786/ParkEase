using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Queries.Parking;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Models;
using ParkingApp.Marketplace.Infrastructure;
using ParkingApp.Marketplace.Infrastructure.Services;

namespace ParkingApp.UnitTests.Infrastructure;

public class OsrmTimeoutTests
{
    private const double OriginLat = 12.9716;
    private const double OriginLng = 77.5946;
    private const double DestLat = 12.9750;
    private const double DestLng = 77.5950;

    private static readonly Uri BaseAddress = new("https://router.project-osrm.org/");

    [Fact]
    public void TypedClient_UsesOneSecondTimeout_AndLeavesOsrmEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Routing:UseOsrmOnSearch"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMarketplaceInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        using var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(typeof(IRoutingService).Name);

        OSRMService.HttpTimeout.Should().Be(TimeSpan.FromSeconds(1));
        client.Timeout.Should().Be(TimeSpan.FromSeconds(1));
        provider.GetRequiredService<IOptions<RoutingOptions>>().Value.UseOsrmOnSearch.Should().BeTrue();
    }

    [Fact]
    public async Task FastOsrmResponse_StillReturnsRoadDistance()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(OkOsrm(1000, 120)));
        using var service = CreateService(handler, OSRMService.HttpTimeout);

        var results = await service.Routing.GetBatchRoutingAsync(OriginLat, OriginLng, OneDestination());

        results.Should().ContainSingle();
        results[0].Distance.Should().Be(1.0);
        results[0].Duration.Should().Be(2);
        results[0].Should().NotBe(Haversine());
        handler.Calls.Should().Be(1);
        handler.LastRequestUri!.AbsolutePath.Should().Contain("table/v1/driving/");
        handler.LastRequestUri.Query.Should().Contain("sources=0");
        handler.LastRequestUri.Query.Should().Contain("annotations=distance,duration");
    }

    [Fact]
    public async Task SlowOsrmResponse_TimesOut_AndReturnsHaversine()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return OkOsrm(999_000, 9_999);
        });
        using var service = CreateService(handler, OSRMService.HttpTimeout);
        using var caller = new CancellationTokenSource();

        var started = Stopwatch.StartNew();
        var results = await service.Routing.GetBatchRoutingAsync(
            OriginLat, OriginLng, OneDestination(), caller.Token);
        started.Stop();

        results.Should().ContainSingle().Which.Should().Be(Haversine());
        handler.Calls.Should().Be(1);
        caller.IsCancellationRequested.Should().BeFalse();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task HttpFailure_StillFallsBackOnce()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"code":"InvalidUrl"}""", Encoding.UTF8, "application/json")
        }));
        using var service = CreateService(handler, OSRMService.HttpTimeout);

        var results = await service.Routing.GetBatchRoutingAsync(OriginLat, OriginLng, OneDestination());

        results.Should().ContainSingle().Which.Should().Be(Haversine());
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task MalformedOsrmBody_StillFallsBackOnce()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }));
        using var service = CreateService(handler, OSRMService.HttpTimeout);

        var results = await service.Routing.GetBatchRoutingAsync(OriginLat, OriginLng, OneDestination());

        results.Should().ContainSingle().Which.Should().Be(Haversine());
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task AlreadyCancelledCaller_ReturnsHaversine_WithoutThrowing()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return OkOsrm(1000, 120);
        });
        using var service = CreateService(handler, OSRMService.HttpTimeout);
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        var act = async () => await service.Routing.GetBatchRoutingAsync(
            OriginLat, OriginLng, OneDestination(), caller.Token);

        var results = (await act.Should().NotThrowAsync()).Subject;
        results.Should().ContainSingle().Which.Should().Be(Haversine());
        handler.Calls.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task CallerCancellationDuringRequest_ReturnsHaversine_WithoutThrowing()
    {
        using var caller = new CancellationTokenSource();
        var handler = new ScriptedHandler(async (_, token) =>
        {
            caller.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return OkOsrm(1000, 120);
        });
        using var service = CreateService(handler, OSRMService.HttpTimeout);

        var act = async () => await service.Routing.GetBatchRoutingAsync(
            OriginLat, OriginLng, OneDestination(), caller.Token);

        var results = (await act.Should().NotThrowAsync()).Subject;
        results.Should().ContainSingle().Which.Should().Be(Haversine());
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Search_WhenOsrmTimesOut_SucceedsWithHaversineDistance()
    {
        const double originLat = 18.52;
        const double originLng = 73.85;
        const double destLat = 18.53;
        const double destLng = 73.86;

        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return OkOsrm(999_000, 9_999);
        });
        using var routed = CreateService(handler, OSRMService.HttpTimeout);

        var bookings = new Mock<IBookingRepository>();
        bookings
            .Setup(r => r.GetDiscoveryBookingAvailabilityAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BookingAvailabilityRead>());
        var uow = new Mock<IMarketplaceUnitOfWork>();
        uow.Setup(u => u.Bookings).Returns(bookings.Object);

        var readStore = new Mock<IParkingReadStore>();
        readStore
            .Setup(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpace>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Lot",
                    TotalSpots = 1,
                    Latitude = destLat,
                    Longitude = destLng
                }
            });
        readStore
            .Setup(r => r.CountSearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cache = new Mock<ICacheService>();
        cache
            .Setup(c => c.GetAsync<ParkingSearchResultDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSearchResultDto?)null);
        cache
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<ParkingSearchResultDto>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var search = new SearchParkingHandler(
            uow.Object,
            readStore.Object,
            cache.Object,
            routed.Routing,
            new TestOptionsMonitor<MarketplaceDiscoveryOptions>(new MarketplaceDiscoveryOptions()),
            new TestOptionsMonitor<RoutingOptions>(new RoutingOptions { UseOsrmOnSearch = true }),
            NullLogger<SearchParkingHandler>.Instance);

        var dto = new ParkingSearchDto
        {
            City = "Pune",
            Latitude = originLat,
            Longitude = originLng,
            Page = 1,
            PageSize = 10
        };
        var canonical = DiscoverySearchCanonical.Apply(dto);
        var expected = OSRMService.HaversineKmAndMinutes(
            canonical.Latitude!.Value,
            canonical.Longitude!.Value,
            destLat,
            destLng);

        var started = Stopwatch.StartNew();
        var result = await search.HandleAsync(new SearchParkingQuery(dto));
        started.Stop();

        result.Success.Should().BeTrue();
        result.Data!.ParkingSpaces.Should().ContainSingle();
        result.Data.ParkingSpaces[0].DistanceKm.Should().Be(expected.DistanceKm);
        result.Data.ParkingSpaces[0].EstimatedTimeMinutes.Should().Be(expected.DurationMinutes);
        handler.Calls.Should().Be(1);
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    private static (double Distance, int Duration) Haversine() =>
        OSRMService.HaversineKmAndMinutes(OriginLat, OriginLng, DestLat, DestLng);

    private static List<(double Lat, double Lng)> OneDestination() => [(DestLat, DestLng)];

    private static RoutedClient CreateService(ScriptedHandler handler, TimeSpan timeout)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = BaseAddress,
            Timeout = timeout
        };
        return new RoutedClient(new OSRMService(httpClient, NullLogger<OSRMService>.Instance), httpClient);
    }

    private static HttpResponseMessage OkOsrm(double meters, double seconds)
    {
        var json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"code\":\"Ok\",\"distances\":[[{0}]],\"durations\":[[{1}]]}}",
            meters,
            seconds);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RoutedClient : IDisposable
    {
        public RoutedClient(OSRMService routing, HttpClient httpClient)
        {
            Routing = routing;
            _httpClient = httpClient;
        }

        public OSRMService Routing { get; }
        private readonly HttpClient _httpClient;

        public void Dispose() => _httpClient.Dispose();
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
            _send = send;

        public int Calls { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastRequestUri = request.RequestUri;
            return await _send(request, cancellationToken);
        }
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T current) => CurrentValue = current;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
