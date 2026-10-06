using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Parking;
using Xunit;

namespace ParkingApp.UnitTests.Caching;

public class DiscoveryFilterParityTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ParkingReadStore _store;
    private readonly Guid _openEvId;
    private readonly Guid _coveredBikeId;
    private readonly Guid _drivewayEvId;
    private readonly Guid _garageId;

    public DiscoveryFilterParityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _store = new ParkingReadStore(
            _context,
            new Mock<ISqlConnectionFactory>().Object,
            new TestOptionsMonitor<MarketplaceDiscoveryOptions>(new MarketplaceDiscoveryOptions()));

        _openEvId = Seed("Open EV", ParkingType.Open, "Car,SUV", 4.8, hasEv: true, ListingCategory.Commercial);
        _coveredBikeId = Seed("Covered bike", ParkingType.Covered, "Motorcycle", 3.2, hasEv: false, ListingCategory.Residential);
        _drivewayEvId = Seed("Driveway EV", ParkingType.Open, null, 4.6, hasEv: true, ListingCategory.Residential);
        _garageId = Seed("Garage", ParkingType.Garage, "Truck", 4.9, hasEv: false, ListingCategory.Commercial);
        Seed("Inactive", ParkingType.Open, "Car", 5, hasEv: true, ListingCategory.Commercial, active: false);
        Seed("Corporate", ParkingType.Open, "Car", 5, hasEv: true, ListingCategory.Commercial, corporateOnly: true);
        _context.SaveChanges();
    }

    [Fact]
    public async Task ListAndCount_ApplyParkingType_AndMapSqlUsesTheSamePredicate()
    {
        var criteria = new ParkingSearchDto(ParkingType: ParkingType.Open, PageSize: 20);

        var ids = await Ids(criteria);
        var count = await _store.CountSearchAsync(criteria);

        ids.Should().BeEquivalentTo(new[] { _openEvId, _drivewayEvId });
        count.Should().Be(ids.Count);
        var map = ParkingReadStore.BuildMapPinsQuery(criteria, maxPins: 500);
        map.Sql.Should().Contain("""AND "ParkingType" = @ParkingType""");
        map.Parameters["ParkingType"].Should().Be((int)ParkingType.Open);
    }

    [Fact]
    public async Task ListAndCount_ApplyVehicleType_IncludingUnrestrictedLots_AndMapSqlMatches()
    {
        var motorcycle = new ParkingSearchDto(VehicleType: VehicleType.Motorcycle, PageSize: 20);
        var car = new ParkingSearchDto(VehicleType: VehicleType.Car, PageSize: 20);

        var motorcycleIds = await Ids(motorcycle);
        var carIds = await Ids(car);

        motorcycleIds.Should().BeEquivalentTo(new[] { _coveredBikeId, _drivewayEvId });
        carIds.Should().BeEquivalentTo(new[] { _openEvId, _drivewayEvId });
        (await _store.CountSearchAsync(motorcycle)).Should().Be(motorcycleIds.Count);
        (await _store.CountSearchAsync(car)).Should().Be(carIds.Count);

        var map = ParkingReadStore.BuildMapPinsQuery(motorcycle, maxPins: 500);
        map.Sql.Should().Contain("""AND ("AllowedVehicleTypes" IS NULL OR "AllowedVehicleTypes" LIKE '%' || @VehicleType || '%')""");
        map.Parameters["VehicleType"].Should().Be(VehicleType.Motorcycle.ToString());
    }

    [Fact]
    public async Task ListAndCount_ApplyMinRating_AndMapSqlMatches()
    {
        var criteria = new ParkingSearchDto(MinRating: 4.7, PageSize: 20);

        var ids = await Ids(criteria);

        ids.Should().BeEquivalentTo(new[] { _openEvId, _garageId });
        (await _store.CountSearchAsync(criteria)).Should().Be(2);
        var map = ParkingReadStore.BuildMapPinsQuery(criteria, maxPins: 500);
        map.Sql.Should().Contain("""AND "AverageRating" >= @MinRating""");
        map.Parameters["MinRating"].Should().Be(4.7);
    }

    [Fact]
    public async Task ListAndCount_ApplyEvOnlyWhenTrue_AndMapSqlMatches()
    {
        var ev = new ParkingSearchDto(HasEvCharging: true, PageSize: 20);
        var notRequired = new ParkingSearchDto(HasEvCharging: false, PageSize: 20);
        var unspecified = new ParkingSearchDto(PageSize: 20);

        var evIds = await Ids(ev);
        var notRequiredIds = await Ids(notRequired);
        var unspecifiedIds = await Ids(unspecified);

        evIds.Should().BeEquivalentTo(new[] { _openEvId, _drivewayEvId });
        notRequiredIds.Should().BeEquivalentTo(unspecifiedIds);
        notRequiredIds.Should().BeEquivalentTo(new[] { _openEvId, _coveredBikeId, _drivewayEvId, _garageId });
        (await _store.CountSearchAsync(ev)).Should().Be(2);
        (await _store.CountSearchAsync(notRequired)).Should().Be(notRequiredIds.Count);

        var evMap = ParkingReadStore.BuildMapPinsQuery(ev, maxPins: 500);
        evMap.Sql.Should().Contain("""AND "HasEvCharging" = TRUE""");
        var plainMap = ParkingReadStore.BuildMapPinsQuery(notRequired, maxPins: 500);
        plainMap.Sql.Should().NotContain("HasEvCharging");
        ParkingReadStore.BuildMapPinsQuery(unspecified, maxPins: 500).Sql.Should().NotContain("HasEvCharging");
    }

    [Fact]
    public async Task ListAndCount_ResolveListingCategory_AndMapSqlMatches()
    {
        var residentialFlag = new ParkingSearchDto(IsResidential: true, PageSize: 20);
        var residentialCategory = new ParkingSearchDto(ListingCategory: ListingCategory.Residential, PageSize: 20);
        var commercialFlag = new ParkingSearchDto(IsResidential: false, PageSize: 20);
        var categoryWins = new ParkingSearchDto(
            IsResidential: true,
            ListingCategory: ListingCategory.Commercial,
            PageSize: 20);

        var residentialIds = await Ids(residentialFlag);
        residentialIds.Should().BeEquivalentTo(await Ids(residentialCategory));
        residentialIds.Should().BeEquivalentTo(new[] { _coveredBikeId, _drivewayEvId });
        (await Ids(commercialFlag)).Should().BeEquivalentTo(new[] { _openEvId, _garageId });
        (await Ids(categoryWins)).Should().BeEquivalentTo(await Ids(commercialFlag));
        (await _store.CountSearchAsync(residentialFlag)).Should().Be(2);
        (await _store.CountSearchAsync(residentialCategory)).Should().Be(2);

        var residentialMap = ParkingReadStore.BuildMapPinsQuery(residentialFlag, maxPins: 500);
        var explicitMap = ParkingReadStore.BuildMapPinsQuery(residentialCategory, maxPins: 500);
        residentialMap.Parameters["ListingCategory"].Should().Be((int)ListingCategory.Residential);
        explicitMap.Parameters["ListingCategory"].Should().Be((int)ListingCategory.Residential);
        residentialMap.Sql.Should().Contain("""AND "ListingCategory" = @ListingCategory""");
        ParkingReadStore.BuildMapPinsQuery(categoryWins, maxPins: 500).Parameters["ListingCategory"]
            .Should().Be((int)ListingCategory.Commercial);
    }

    [Fact]
    public void MapSql_UsesTheSameCanonicalGeographyAsTheCacheKey()
    {
        var raw = new ParkingSearchDto(Latitude: 18.52041, Longitude: 73.85671, RadiusKm: 5.04);
        var sameBucket = new ParkingSearchDto(Latitude: 18.52044, Longitude: 73.85672, RadiusKm: 5.0);
        var nextBucket = new ParkingSearchDto(Latitude: 18.52045, Longitude: 73.85671, RadiusKm: 1.35);

        var first = ParkingReadStore.BuildMapPinsQuery(raw, maxPins: 500);
        var second = ParkingReadStore.BuildMapPinsQuery(sameBucket, maxPins: 500);
        var third = ParkingReadStore.BuildMapPinsQuery(nextBucket, maxPins: 500);

        first.Sql.Should().Contain("ST_DWithin");
        first.Parameters["Lat"].Should().Be(CacheKeys.CanonicalCoordinate(18.52041));
        first.Parameters["Lng"].Should().Be(CacheKeys.CanonicalCoordinate(73.85671));
        first.Parameters["RadiusM"].Should().Be(5000d);
        second.Parameters["Lat"].Should().Be(first.Parameters["Lat"]);
        second.Parameters["Lng"].Should().Be(first.Parameters["Lng"]);
        second.Parameters["RadiusM"].Should().Be(first.Parameters["RadiusM"]);
        third.Parameters["Lat"].Should().NotBe(first.Parameters["Lat"]);
        third.Parameters["RadiusM"].Should().Be(1400d);
    }

    public void Dispose() => _context.Dispose();

    private async Task<List<Guid>> Ids(ParkingSearchDto criteria)
    {
        var rows = await _store.SearchAsync(criteria);
        return rows.Select(row => row.Id).ToList();
    }

    private Guid Seed(
        string title,
        ParkingType parkingType,
        string? vehicles,
        double rating,
        bool hasEv,
        ListingCategory category,
        bool active = true,
        bool corporateOnly = false)
    {
        var id = Guid.NewGuid();
        _context.ParkingSpaces.Add(new ParkingSpace
        {
            Id = id,
            OwnerId = Guid.NewGuid(),
            Title = title,
            Description = "D",
            Address = "A",
            City = "Pune",
            State = "MH",
            Country = "IN",
            PostalCode = "411001",
            ParkingType = parkingType,
            AllowedVehicleTypes = vehicles,
            AverageRating = rating,
            HasEvCharging = hasEv,
            ListingCategory = category,
            IsActive = active,
            IsCorporateOnly = corporateOnly,
            TotalSpots = 2,
            HourlyRate = 40
        });
        return id;
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
