using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Queries.Parking;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Models;
using Xunit;

namespace ParkingApp.UnitTests.Caching;

public class DiscoveryCacheContractTests
{
    private readonly Mock<IParkingReadStore> _readStore = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly RecordingCache _cache = new();
    private readonly Mock<IRoutingService> _routing = new();

    public DiscoveryCacheContractTests()
    {
        _bookings
            .Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());
        _bookings
            .Setup(r => r.GetDiscoveryBookingAvailabilityAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BookingAvailabilityRead>());
        _readStore
            .Setup(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSearchDto dto, CancellationToken _) => new List<ParkingSpace>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = dto.ParkingType?.ToString() ?? "any",
                    TotalSpots = 1,
                    Latitude = 18.53,
                    Longitude = 73.86
                }
            });
        _readStore
            .Setup(r => r.CountSearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _routing
            .Setup(r => r.GetBatchRoutingAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<List<(double, double)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(double, int)> { (1.5, 4) });
        _routing
            .Setup(r => r.GetBatchHaversine(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<List<(double, double)>>()))
            .Returns(new List<(double, int)> { (1.1, 3) });
    }

    [Fact]
    public void SearchKey_IsolatesOutputAffectingInputs()
    {
        var baseline = Baseline();
        var baselineKey = SearchKey(baseline);

        SearchKey(baseline with { HasEvCharging = true }).Should().NotBe(baselineKey);
        SearchKey(baseline with { IsResidential = null, ListingCategory = ListingCategory.Residential }).Should().NotBe(baselineKey);
        SearchKey(baseline with { IsResidential = true, ListingCategory = null }).Should().NotBe(baselineKey);
        SearchKey(baseline with { MinRating = 3 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { ParkingType = ParkingType.Covered }).Should().NotBe(baselineKey);
        SearchKey(baseline with { VehicleType = VehicleType.Motorcycle }).Should().NotBe(baselineKey);
        SearchKey(baseline with { Latitude = 19.0760 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { Longitude = 72.8777 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { RadiusKm = 8 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { Page = 2 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { PageSize = 20 }).Should().NotBe(baselineKey);
        SearchKey(baseline with { SortBy = "rating" }).Should().NotBe(baselineKey);
        SearchKey(baseline with { SortDescending = true }).Should().NotBe(baselineKey);
    }

    [Fact]
    public void MapKey_IsolatesOutputAffectingInputs_AndIgnoresPaging()
    {
        var baseline = Baseline();
        var baselineKey = MapKey(baseline);

        MapKey(baseline with { HasEvCharging = true }).Should().NotBe(baselineKey);
        MapKey(baseline with { IsResidential = null, ListingCategory = ListingCategory.Residential }).Should().NotBe(baselineKey);
        MapKey(baseline with { IsResidential = true, ListingCategory = null }).Should().NotBe(baselineKey);
        MapKey(baseline with { MinRating = 3 }).Should().NotBe(baselineKey);
        MapKey(baseline with { MinRating = 4.5 }).Should().NotBe(MapKey(baseline with { MinRating = 3.0 }));
        MapKey(baseline with { ParkingType = ParkingType.Covered }).Should().NotBe(baselineKey);
        MapKey(baseline with { VehicleType = VehicleType.Motorcycle }).Should().NotBe(baselineKey);
        MapKey(baseline with { Latitude = 19.0760 }).Should().NotBe(baselineKey);
        MapKey(baseline with { Longitude = 72.8777 }).Should().NotBe(baselineKey);
        MapKey(baseline with { RadiusKm = 8 }).Should().NotBe(baselineKey);
        MapKey(baseline with { Page = 2 }).Should().Be(baselineKey);
        MapKey(baseline with { PageSize = 50 }).Should().Be(baselineKey);
        MapKey(baseline with { SortBy = "distance" }).Should().Be(baselineKey);
    }

    [Fact]
    public void Keys_CanonicalizeEquivalentFilters_AndIgnoreNonParticipatingFields()
    {
        var baseline = Baseline();

        SearchKey(baseline with { HasEvCharging = null }).Should().Be(SearchKey(baseline with { HasEvCharging = false }));
        MapKey(baseline with { HasEvCharging = null }).Should().Be(MapKey(baseline with { HasEvCharging = false }));
        SearchKey(baseline with { HasEvCharging = true }).Should().NotBe(SearchKey(baseline with { HasEvCharging = false }));

        var residential = baseline with { IsResidential = true, ListingCategory = null };
        var explicitResidential = baseline with { IsResidential = null, ListingCategory = ListingCategory.Residential };
        SearchKey(residential).Should().Be(SearchKey(explicitResidential));
        MapKey(residential).Should().Be(MapKey(explicitResidential));

        var commercial = baseline with { IsResidential = false, ListingCategory = null };
        var explicitCommercial = baseline with { IsResidential = null, ListingCategory = ListingCategory.Commercial };
        SearchKey(commercial).Should().Be(SearchKey(explicitCommercial));
        MapKey(baseline with { IsResidential = true, ListingCategory = ListingCategory.Commercial })
            .Should().Be(MapKey(explicitCommercial));

        SearchKey(baseline with { Latitude = 18.52041, Longitude = 73.85671, RadiusKm = 5.04 })
            .Should().Be(SearchKey(baseline with { Latitude = 18.52044, Longitude = 73.85674, RadiusKm = 5.0 }));
        SearchKey(baseline with { Latitude = 18.52044 })
            .Should().NotBe(SearchKey(baseline with { Latitude = 18.52045 }));
        SearchKey(baseline with { RadiusKm = 1.25 }).Should().Be(SearchKey(baseline with { RadiusKm = 1.2 }));
        SearchKey(baseline with { RadiusKm = 1.25 }).Should().NotBe(SearchKey(baseline with { RadiusKm = 1.35 }));

        var endA = baseline with { EndDateTime = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc) };
        var endB = baseline with { EndDateTime = new DateTime(2026, 10, 6, 18, 0, 0, DateTimeKind.Utc) };
        SearchKey(endA).Should().Be(SearchKey(endB));
        MapKey(endA).Should().Be(MapKey(endB));
        SearchKey(baseline with { PricingType = PricingType.Hourly })
            .Should().Be(SearchKey(baseline with { PricingType = PricingType.Daily }));
        MapKey(baseline with { PricingType = PricingType.Hourly })
            .Should().Be(MapKey(baseline with { PricingType = null }));

        SearchKey(baseline with { Amenities = new List<string> { "CCTV", "Covered" } })
            .Should().Be(SearchKey(baseline with { Amenities = new List<string> { "Covered", "CCTV" } }));
    }

    [Fact]
    public void Keys_AreCultureIndependent()
    {
        var dto = Baseline() with
        {
            MinPrice = 10.5m,
            MaxPrice = 20.25m,
            Latitude = 18.5,
            Longitude = 73.85,
            RadiusKm = 5.5,
            Amenities = new List<string> { "i", "I" }
        };

        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            var turkish = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkish;
            CultureInfo.CurrentUICulture = turkish;
            var turkishSearch = SearchKey(dto);
            var turkishMap = MapKey(dto);

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            turkishSearch.Should().Be(SearchKey(dto));
            turkishMap.Should().Be(MapKey(dto));
            turkishSearch.Should().Contain("10.5");
            turkishSearch.Should().NotContain("10,5");
            turkishSearch.Should().Contain("ev:0");
            turkishSearch.Should().Contain("cat:Commercial");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public async Task Search_WhenStartDateTimeSupplied_BypassesCacheReadAndWrite()
    {
        var handler = CreateSearchHandler();
        var dto = Baseline() with { StartDateTime = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc) };

        var first = await handler.HandleAsync(new SearchParkingQuery(dto));
        var second = await handler.HandleAsync(new SearchParkingQuery(dto));

        first.Success.Should().BeTrue();
        second.Data!.ParkingSpaces[0].Id.Should().NotBe(first.Data!.ParkingSpaces[0].Id);
        _cache.Gets.Should().BeEmpty();
        _cache.Sets.Should().BeEmpty();
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Map_WhenStartDateTimeSupplied_BypassesCacheReadAndWrite()
    {
        var pin = new ParkingMapDto(Guid.NewGuid(), "Pin", "Addr", "Pune", 18.52, 73.85, 40, null, 4, ParkingType.Open);
        _readStore
            .Setup(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingMapDto> { pin });
        var handler = CreateMapHandler();
        var dto = Baseline() with { StartDateTime = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc) };

        var result = await handler.HandleAsync(new GetMapCoordinatesQuery(dto));

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle();
        _cache.Gets.Should().BeEmpty();
        _cache.Sets.Should().BeEmpty();
        _readStore.Verify(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_CacheHit_MatchesFreshResult_AndDoesNotServeADifferentFilter()
    {
        var handler = CreateSearchHandler();
        var dto = Baseline();

        var fresh = await handler.HandleAsync(new SearchParkingQuery(dto));
        var hit = await handler.HandleAsync(new SearchParkingQuery(dto));
        var other = await handler.HandleAsync(new SearchParkingQuery(dto with { ParkingType = ParkingType.Covered }));

        hit.Data!.ParkingSpaces[0].Id.Should().Be(fresh.Data!.ParkingSpaces[0].Id);
        hit.Data.TotalCount.Should().Be(fresh.Data.TotalCount);
        other.Data!.ParkingSpaces[0].Id.Should().NotBe(fresh.Data.ParkingSpaces[0].Id);
        other.Data.ParkingSpaces[0].Title.Should().Be("Covered");
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _cache.Sets.Should().HaveCount(2);
        _cache.Sets.Should().OnlyContain(set => set.Expiry == TimeSpan.FromMinutes(5));
        _cache.Gets.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("ev")]
    [InlineData("rating")]
    [InlineData("vehicle")]
    [InlineData("category")]
    [InlineData("page")]
    [InlineData("geo")]
    public async Task Search_CachedResult_IsNotReusedWhenAnOutputAffectingInputChanges(string dimension)
    {
        var handler = CreateSearchHandler();
        var dto = Baseline();
        var fresh = await handler.HandleAsync(new SearchParkingQuery(dto));
        var changed = dimension switch
        {
            "ev" => dto with { HasEvCharging = true },
            "rating" => dto with { MinRating = 3 },
            "vehicle" => dto with { VehicleType = VehicleType.Motorcycle },
            "category" => dto with { IsResidential = true, ListingCategory = null },
            "page" => dto with { Page = 2 },
            "geo" => dto with { Latitude = 19.0760 },
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null)
        };

        var other = await handler.HandleAsync(new SearchParkingQuery(changed));

        other.Data!.ParkingSpaces[0].Id.Should().NotBe(fresh.Data!.ParkingSpaces[0].Id);
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Map_CacheHit_MatchesFreshPins_AndIsolatesMinRating()
    {
        _readStore
            .Setup(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSearchDto dto, CancellationToken _) =>
                new List<ParkingMapDto>
                {
                    new(Guid.NewGuid(), dto.MinRating?.ToString(CultureInfo.InvariantCulture) ?? "none", "Addr", "Pune", 18.52, 73.85, 40, null, dto.MinRating ?? 0, ParkingType.Open)
                });
        var handler = CreateMapHandler();
        var dto = Baseline() with { MinRating = 4.5 };

        var fresh = await handler.HandleAsync(new GetMapCoordinatesQuery(dto));
        var hit = await handler.HandleAsync(new GetMapCoordinatesQuery(dto));
        var other = await handler.HandleAsync(new GetMapCoordinatesQuery(dto with { MinRating = 3.0 }));

        hit.Data![0].Id.Should().Be(fresh.Data![0].Id);
        other.Data![0].Id.Should().NotBe(fresh.Data[0].Id);
        other.Data[0].Title.Should().Be("3");
        _readStore.Verify(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _cache.Sets.Should().OnlyContain(set => set.Expiry == TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Search_QueryUsesCanonicalGeography_AndSameBucketSharesTheCacheEntry()
    {
        ParkingSearchDto? captured = null;
        _readStore
            .Setup(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .Callback<ParkingSearchDto, CancellationToken>((dto, _) => captured = dto)
            .ReturnsAsync((ParkingSearchDto dto, CancellationToken _) => new List<ParkingSpace>
            {
                new() { Id = Guid.NewGuid(), Title = "A", TotalSpots = 1, Latitude = 18.53, Longitude = 73.86 }
            });
        var handler = CreateSearchHandler();
        var firstDto = Baseline() with { Latitude = 18.52041, Longitude = 73.85671, RadiusKm = 5.04 };
        var sameBucket = Baseline() with { Latitude = 18.52044, Longitude = 73.85672, RadiusKm = 5.0 };

        var first = await handler.HandleAsync(new SearchParkingQuery(firstDto));
        var second = await handler.HandleAsync(new SearchParkingQuery(sameBucket));

        captured!.Latitude.Should().Be(CacheKeys.CanonicalCoordinate(18.52041));
        captured.Longitude.Should().Be(CacheKeys.CanonicalCoordinate(73.85671));
        captured.RadiusKm.Should().Be(CacheKeys.CanonicalRadius(5.04));
        second.Data!.ParkingSpaces[0].Id.Should().Be(first.Data!.ParkingSpaces[0].Id);
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _routing.Verify(r => r.GetBatchRoutingAsync(
            captured.Latitude!.Value,
            captured.Longitude!.Value,
            It.IsAny<List<(double, double)>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Map_QueryUsesCanonicalGeography()
    {
        ParkingSearchDto? captured = null;
        _readStore
            .Setup(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .Callback<ParkingSearchDto, CancellationToken>((dto, _) => captured = dto)
            .ReturnsAsync(new List<ParkingMapDto>());
        var handler = CreateMapHandler();

        await handler.HandleAsync(new GetMapCoordinatesQuery(
            Baseline() with { Latitude = 18.52041, Longitude = 73.85671, RadiusKm = 1.35 }));

        captured!.Latitude.Should().Be(CacheKeys.CanonicalCoordinate(18.52041));
        captured.Longitude.Should().Be(CacheKeys.CanonicalCoordinate(73.85671));
        captured.RadiusKm.Should().Be(1.4);
    }

    [Fact]
    public async Task Search_EndDateTimeAndPricingType_StillUseTheCache()
    {
        var handler = CreateSearchHandler();
        var first = Baseline() with
        {
            EndDateTime = new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc),
            PricingType = PricingType.Hourly
        };
        var second = first with
        {
            EndDateTime = new DateTime(2026, 10, 6, 20, 0, 0, DateTimeKind.Utc),
            PricingType = PricingType.Monthly
        };

        var fresh = await handler.HandleAsync(new SearchParkingQuery(first));
        var hit = await handler.HandleAsync(new SearchParkingQuery(second));

        hit.Data!.ParkingSpaces[0].Id.Should().Be(fresh.Data!.ParkingSpaces[0].Id);
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Sets.Should().ContainSingle();
        _cache.Gets.Should().HaveCount(2);
    }

    [Fact]
    public async Task BookingChange_DoesNotInvalidateDiscovery_WhileListingAndReviewChangesDo()
    {
        var cache = new Mock<ICacheService>();
        var parkingId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await CacheInvalidation.ForBookingChangeAsync(cache.Object, parkingId, memberId, ownerId);

        cache.Verify(c => c.RemoveAsync(CacheKeys.Parking(parkingId), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.SearchAll, It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.MapAll, It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.ParkingForecastAll, It.IsAny<CancellationToken>()), Times.Once);

        await CacheInvalidation.ForReviewChangeAsync(cache.Object, parkingId, ownerId);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.SearchAll, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.MapAll, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(CacheKeys.Reviews(parkingId), It.IsAny<CancellationToken>()), Times.Once);

        await CacheInvalidation.ForParkingMutationAsync(cache.Object, parkingId, ownerId);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.SearchAll, It.IsAny<CancellationToken>()), Times.Exactly(2));
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.MapAll, It.IsAny<CancellationToken>()), Times.Exactly(2));
        cache.Verify(c => c.RemoveAsync(CacheKeys.Parking(parkingId), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Search_KeepsCachedDiscoveryAfterABookingWouldHaveChangedLiveReservations()
    {
        var handler = CreateSearchHandler();
        var dto = Baseline();

        var fresh = await handler.HandleAsync(new SearchParkingQuery(dto));
        var stillCached = await handler.HandleAsync(new SearchParkingQuery(dto));

        stillCached.Data!.ParkingSpaces[0].Id.Should().Be(fresh.Data!.ParkingSpaces[0].Id);
        _readStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Sets.Should().ContainSingle();
        _cache.Sets[0].Expiry.Should().Be(TimeSpan.FromMinutes(5));
    }

    private SearchParkingHandler CreateSearchHandler()
    {
        var uow = new Mock<IMarketplaceUnitOfWork>();
        uow.Setup(u => u.Bookings).Returns(_bookings.Object);
        return new SearchParkingHandler(
            uow.Object,
            _readStore.Object,
            _cache,
            _routing.Object,
            Options(),
            new TestOptionsMonitor<RoutingOptions>(new RoutingOptions { UseOsrmOnSearch = true }),
            new Mock<ILogger<SearchParkingHandler>>().Object);
    }

    private GetMapCoordinatesHandler CreateMapHandler() =>
        new(_readStore.Object, _cache, Options());

    private static IOptionsMonitor<MarketplaceDiscoveryOptions> Options() =>
        new TestOptionsMonitor<MarketplaceDiscoveryOptions>(new MarketplaceDiscoveryOptions());

    private static string SearchKey(ParkingSearchDto dto) =>
        SearchParkingHandler.BuildSearchCacheKey(DiscoverySearchCanonical.Apply(dto), useOsrmOnSearch: true);

    private static string MapKey(ParkingSearchDto dto) =>
        GetMapCoordinatesHandler.BuildMapCacheKey(DiscoverySearchCanonical.Apply(dto));

    private static ParkingSearchDto Baseline() => new(
        State: "MH",
        City: "Pune",
        Address: "FC Road",
        Latitude: 18.5204,
        Longitude: 73.8567,
        RadiusKm: 5,
        MinPrice: 10,
        MaxPrice: 100,
        ParkingType: ParkingType.Open,
        VehicleType: VehicleType.Car,
        Amenities: new List<string> { "CCTV", "Covered" },
        MinRating: 4,
        SortBy: "price",
        SortDescending: false,
        Page: 1,
        PageSize: 12,
        HasEvCharging: false,
        IsResidential: false);

    private sealed class RecordingCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public List<string> Gets { get; } = new();
        public List<(string Key, TimeSpan? Expiry)> Sets { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            Gets.Add(key);
            if (_values.TryGetValue(key, out var value) && value is T typed)
                return Task.FromResult<T?>(typed);
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            Sets.Add((key, expiry));
            _values[key] = value!;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
