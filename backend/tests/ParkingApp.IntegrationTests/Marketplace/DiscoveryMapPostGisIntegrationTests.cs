using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Npgsql;
using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Infrastructure.Data;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Queries.Parking;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.IntegrationTests.Marketplace;

/// <summary>
/// Test-only. Executes the production map-pin read path against the existing
/// FullApi PostGIS container. No production behavior is changed.
/// </summary>
[Collection(FullApiHttpCollection.Name)]
[Trait("Layer", "PostGIS")]
[Trait("Feature", "DiscoveryMap")]
public sealed class DiscoveryMapPostGisIntegrationTests : IAsyncLifetime
{
    private const double RawLatitude = 18.52041;
    private const double RawLongitude = 73.85671;
    private const double RawTightRadiusKm = 0.14;
    private const double RawWideRadiusKm = 0.15;
    private const double NegativeRawLatitude = -33.86881;
    private const double NegativeRawLongitude = -70.66926;

    private readonly FullApiPostgresFixture _postgres;
    private readonly FullApiFactory _factory;
    private readonly HttpClient _client;
    private readonly string _state;
    private readonly string _geoCity;
    private readonly string _filterCity;

    private Guid _nearId;
    private Guid _edgeId;
    private Guid _mid120Id;
    private Guid _mid170Id;
    private Guid _farId;
    private Guid _deletedId;
    private Guid _inactiveId;
    private Guid _corporateId;
    private Guid _negativeNearId;
    private Guid _negativeFarId;
    private Guid _openEvId;
    private Guid _coveredBikeId;
    private Guid _drivewayId;
    private Guid _garageId;

    public DiscoveryMapPostGisIntegrationTests(FullApiPostgresFixture postgres)
    {
        _postgres = postgres;
        _state = "P0" + Guid.NewGuid().ToString("N");
        _geoCity = "G" + Guid.NewGuid().ToString("N")[..12];
        _filterCity = "F" + Guid.NewGuid().ToString("N")[..12];
        _factory = new FullApiFactory(postgres.ConnectionString, channelIsolationEnabled: false);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task InitializeAsync()
    {
        var canonicalLat = CacheKeys.CanonicalCoordinate(RawLatitude)!.Value;
        var canonicalLng = CacheKeys.CanonicalCoordinate(RawLongitude)!.Value;
        var negativeLat = CacheKeys.CanonicalCoordinate(NegativeRawLatitude)!.Value;
        var negativeLng = CacheKeys.CanonicalCoordinate(NegativeRawLongitude)!.Value;

        var edge = await ProjectBeyondCanonicalCenterAsync(canonicalLat, canonicalLng, RawLatitude, RawLongitude);
        var mid120 = await ProjectNorthAsync(canonicalLat, canonicalLng, 120);
        var mid170 = await ProjectNorthAsync(canonicalLat, canonicalLng, 170);
        var far = await ProjectNorthAsync(canonicalLat, canonicalLng, 2000);
        var negativeFar = await ProjectNorthAsync(negativeLat, negativeLng, 500);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _nearId = Add(db, _geoCity, "P0 Near", canonicalLat, canonicalLng);
        _edgeId = Add(db, _geoCity, "P0 Edge", edge.Lat, edge.Lng);
        _mid120Id = Add(db, _geoCity, "P0 Mid120", mid120.Lat, mid120.Lng);
        _mid170Id = Add(db, _geoCity, "P0 Mid170", mid170.Lat, mid170.Lng);
        _farId = Add(db, _geoCity, "P0 Far", far.Lat, far.Lng);
        _deletedId = Add(db, _geoCity, "P0 Deleted", canonicalLat, canonicalLng, deleted: true);
        _inactiveId = Add(db, _geoCity, "P0 Inactive", canonicalLat, canonicalLng, active: false);
        _corporateId = Add(db, _geoCity, "P0 Corporate", canonicalLat, canonicalLng, corporateOnly: true);
        _negativeNearId = Add(db, _geoCity, "P0 Negative Near", negativeLat, negativeLng);
        _negativeFarId = Add(db, _geoCity, "P0 Negative Far", negativeFar.Lat, negativeFar.Lng);

        _openEvId = Add(db, _filterCity, "P0 Open EV", canonicalLat, canonicalLng,
            parkingType: ParkingType.Open, vehicles: "Car,SUV", rating: 4.8, ev: true, hourly: 40);
        _coveredBikeId = Add(db, _filterCity, "P0 Covered Bike", canonicalLat, canonicalLng,
            parkingType: ParkingType.Covered, vehicles: "Motorcycle", rating: 3.2,
            category: ListingCategory.Residential, hourly: 30);
        _drivewayId = Add(db, _filterCity, "P0 Driveway", canonicalLat, canonicalLng,
            parkingType: ParkingType.Open, vehicles: null, rating: 4.6, ev: true,
            category: ListingCategory.Residential, hourly: 50, address: "Driveway Lane 9");
        _garageId = Add(db, _filterCity, "P0 Garage", canonicalLat, canonicalLng,
            parkingType: ParkingType.Garage, vehicles: "Truck", rating: 4.9, hourly: 80,
            amenities: "Covered");

        await db.SaveChangesAsync();

        var edgeFromCanonical = await DistanceMetersAsync(_edgeId, canonicalLat, canonicalLng);
        var edgeFromRaw = await DistanceMetersAsync(_edgeId, RawLatitude, RawLongitude);
        edgeFromCanonical.Should().BeGreaterThan(100).And.BeLessThan(103);
        edgeFromRaw.Should().BeLessThan(100).And.BeGreaterThan(97);

        (await DistanceMetersAsync(_mid120Id, canonicalLat, canonicalLng)).Should().BeInRange(110, 130);
        (await DistanceMetersAsync(_mid170Id, canonicalLat, canonicalLng)).Should().BeInRange(160, 180);
        (await DistanceMetersAsync(_farId, canonicalLat, canonicalLng)).Should().BeGreaterThan(1500);
        (await DistanceMetersAsync(_nearId, canonicalLat, canonicalLng)).Should().BeLessThan(1);
        (await DistanceMetersAsync(_negativeNearId, negativeLat, negativeLng)).Should().BeLessThan(1);
        (await DistanceMetersAsync(_negativeFarId, negativeLat, negativeLng)).Should().BeInRange(450, 550);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "ParkingSpaces" WHERE "State" = {_state}""");
        }
        catch
        {
            // The host may have failed before the database accepted a command.
        }

        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task MapPinQuery_ExecutesOnPostGis_WithCanonicalGeographyAndFilters()
    {
        CacheKeys.CanonicalCoordinate(RawLatitude).Should().Be(18.5204);
        CacheKeys.CanonicalCoordinate(RawLongitude).Should().Be(73.8567);
        CacheKeys.CanonicalRadius(RawTightRadiusKm).Should().Be(0.1);
        CacheKeys.CanonicalRadius(RawWideRadiusKm).Should().Be(0.2);
        CacheKeys.CanonicalCoordinate(NegativeRawLatitude).Should().Be(-33.8688);
        CacheKeys.CanonicalCoordinate(NegativeRawLongitude).Should().Be(-70.6693);

        var tight = Geo();
        var tightIds = await AssertDiscoverySet(
            tight,
            _nearId);
        (await HttpMapIds(tight)).Should().BeEquivalentTo(tightIds);

        var sameBucket = Geo(latitude: 18.52044, longitude: 73.85674);
        (await MapIds(sameBucket)).Should().BeEquivalentTo(tightIds);

        await AssertDiscoverySet(
            Geo(radiusKm: RawWideRadiusKm),
            _nearId, _edgeId, _mid120Id, _mid170Id);

        await AssertDiscoverySet(
            Geo(latitude: NegativeRawLatitude, longitude: NegativeRawLongitude),
            _negativeNearId);

        var open = Filter(ParkingType: ParkingType.Open);
        await AssertDiscoverySet(open, _openEvId, _drivewayId);
        (await HttpMapIds(open)).Should().BeEquivalentTo(new[] { _openEvId, _drivewayId });

        await AssertDiscoverySet(Filter(VehicleType: VehicleType.Motorcycle), _coveredBikeId, _drivewayId);
        await AssertDiscoverySet(Filter(VehicleType: VehicleType.Car), _openEvId, _drivewayId);
        await AssertDiscoverySet(Filter(VehicleType: VehicleType.Truck), _garageId, _drivewayId);
        await AssertDiscoverySet(Filter(MinRating: 4.7), _openEvId, _garageId);
        await AssertDiscoverySet(Filter(HasEvCharging: true), _openEvId, _drivewayId);

        var evOff = await MapIds(Filter(HasEvCharging: false));
        var evUnspecified = await MapIds(Filter());
        evOff.Should().BeEquivalentTo(evUnspecified);
        evOff.Should().BeEquivalentTo(new[] { _openEvId, _coveredBikeId, _drivewayId, _garageId });
        (await ListAndCount(Filter(HasEvCharging: false))).Ids.Should().BeEquivalentTo(evOff);

        await AssertDiscoverySet(Filter(IsResidential: true), _coveredBikeId, _drivewayId);
        await AssertDiscoverySet(Filter(ListingCategory: ListingCategory.Residential), _coveredBikeId, _drivewayId);
        await AssertDiscoverySet(Filter(IsResidential: false), _openEvId, _garageId);
        await AssertDiscoverySet(
            Filter(IsResidential: true, ListingCategory: ListingCategory.Commercial),
            _openEvId, _garageId);
        await AssertDiscoverySet(Filter(MinPrice: 70), _garageId);
        await AssertDiscoverySet(Filter(Address: "Driveway Lane"), _drivewayId);
        await AssertDiscoverySet(Filter(Amenities: new List<string> { "Covered" }), _garageId);

        var key = MapKey(tight);
        key.Should().Contain(":0.1:18.5204:73.8567:");
        key.Should().NotContain("18.52041");
        key.Should().NotContain("73.85671");
        key.Should().NotContain("0.14");
        MapKey(sameBucket).Should().Be(key);
        MapKey(Geo(latitude: 18.52045)).Should().NotBe(key);
        MapKey(Geo(radiusKm: RawWideRadiusKm)).Should().Contain(":0.2:18.5204:73.8567:");

        var negativeKey = MapKey(Geo(latitude: NegativeRawLatitude, longitude: NegativeRawLongitude));
        negativeKey.Should().Contain(":0.1:-33.8688:-70.6693:");
        negativeKey.Should().NotContain("-33.86881");
        negativeKey.Should().NotContain("-70.66926");

        var ignored = tight with
        {
            StartDateTime = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc),
            PricingType = PricingType.Daily,
            Page = 3,
            PageSize = 10,
            SortBy = "price",
            SortDescending = true
        };
        MapKey(ignored).Should().Be(key);
        key.Should().NotContain("2026");
        key.Should().NotContain("Daily");
        key.Should().NotContain("price");

        var evKey = MapKey(Filter(HasEvCharging: true, MinRating: 4.7, ParkingType: ParkingType.Open, VehicleType: VehicleType.Car));
        evKey.Should().Contain(":Open:");
        evKey.Should().Contain(":Car:");
        evKey.Should().Contain(":r:4.7:");
        evKey.Should().Contain(":ev:1:");
        evKey.Should().Contain(":cat:");
        MapKey(Filter(HasEvCharging: false)).Should().Be(MapKey(Filter(HasEvCharging: null)));
        MapKey(Filter(HasEvCharging: false)).Should().Contain(":ev:0:");
        MapKey(Filter(IsResidential: true)).Should().Be(MapKey(Filter(ListingCategory: ListingCategory.Residential)));
        MapKey(Filter(IsResidential: true)).Should().Contain(":cat:Residential");
        MapKey(Filter(IsResidential: true, ListingCategory: ListingCategory.Commercial)).Should().Contain(":cat:Commercial");
    }

    private ParkingSearchDto Geo(double? latitude = null, double? longitude = null, double? radiusKm = null) =>
        new(
            State: _state,
            City: _geoCity,
            Latitude: latitude ?? RawLatitude,
            Longitude: longitude ?? RawLongitude,
            RadiusKm: radiusKm ?? RawTightRadiusKm,
            PageSize: 20);

    private ParkingSearchDto Filter(
        ParkingType? ParkingType = null,
        VehicleType? VehicleType = null,
        double? MinRating = null,
        bool? HasEvCharging = null,
        bool? IsResidential = null,
        ListingCategory? ListingCategory = null,
        decimal? MinPrice = null,
        string? Address = null,
        List<string>? Amenities = null) =>
        new(
            State: _state,
            City: _filterCity,
            Address: Address,
            Latitude: RawLatitude,
            Longitude: RawLongitude,
            RadiusKm: RawTightRadiusKm,
            MinPrice: MinPrice,
            ParkingType: ParkingType,
            VehicleType: VehicleType,
            Amenities: Amenities,
            MinRating: MinRating,
            PageSize: 20,
            HasEvCharging: HasEvCharging,
            IsResidential: IsResidential,
            ListingCategory: ListingCategory);

    private static string MapKey(ParkingSearchDto dto) =>
        GetMapCoordinatesHandler.BuildMapCacheKey(DiscoverySearchCanonical.Apply(dto));

    private async Task<List<Guid>> AssertDiscoverySet(ParkingSearchDto dto, params Guid[] expected)
    {
        var map = await MapIds(dto);
        var listed = await ListAndCount(dto);
        map.Should().BeEquivalentTo(expected);
        listed.Ids.Should().BeEquivalentTo(expected);
        listed.Count.Should().Be(expected.Length);
        return map;
    }

    private async Task<List<Guid>> MapIds(ParkingSearchDto dto)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IParkingReadStore>();
        var pins = await store.GetMapPinsAsync(dto);
        return pins.Select(pin => pin.Id).ToList();
    }

    private async Task<(List<Guid> Ids, int Count)> ListAndCount(ParkingSearchDto dto)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IParkingReadStore>();
        var rows = await store.SearchAsync(dto);
        var count = await store.CountSearchAsync(dto);
        return (rows.Select(row => row.Id).ToList(), count);
    }

    private async Task<List<Guid>> HttpMapIds(ParkingSearchDto dto)
    {
        var parts = new List<string>
        {
            $"state={Uri.EscapeDataString(dto.State!)}",
            $"city={Uri.EscapeDataString(dto.City!)}",
            $"latitude={dto.Latitude!.Value.ToString(CultureInfo.InvariantCulture)}",
            $"longitude={dto.Longitude!.Value.ToString(CultureInfo.InvariantCulture)}",
            $"radiusKm={dto.RadiusKm!.Value.ToString(CultureInfo.InvariantCulture)}",
            "pageSize=20"
        };
        if (dto.ParkingType.HasValue)
            parts.Add($"parkingType={dto.ParkingType.Value}");

        var response = await _client.GetAsync("/api/parking/map?" + string.Join('&', parts));
        var json = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        var body = JsonSerializer.Deserialize<ApiResponse<List<ParkingMapDto>>>(json, HttpApiClientExtensions.JsonOptions);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue(json);
        body.Data.Should().NotBeNull();
        return body.Data!.Select(pin => pin.Id).ToList();
    }

    private Guid Add(
        ApplicationDbContext db,
        string city,
        string title,
        double latitude,
        double longitude,
        ParkingType parkingType = ParkingType.Open,
        string? vehicles = "Car",
        double rating = 4.8,
        bool ev = false,
        ListingCategory category = ListingCategory.Commercial,
        bool active = true,
        bool corporateOnly = false,
        bool deleted = false,
        decimal hourly = 40,
        string? amenities = null,
        string? address = null)
    {
        var space = new ParkingSpace
        {
            OwnerId = Guid.NewGuid(),
            Title = title,
            Description = "P0 PostGIS map verification",
            Address = address ?? "1 Canonical Road",
            City = city,
            State = _state,
            Country = "IN",
            PostalCode = "411001",
            Latitude = latitude,
            Longitude = longitude,
            Location = new Point(longitude, latitude) { SRID = 4326 },
            ParkingType = parkingType,
            AllowedVehicleTypes = vehicles,
            AverageRating = rating,
            HasEvCharging = ev,
            ListingCategory = category,
            IsActive = active,
            IsCorporateOnly = corporateOnly,
            IsDeleted = deleted,
            TotalSpots = 2,
            AvailableSpots = 2,
            HourlyRate = hourly,
            DailyRate = hourly * 8,
            WeeklyRate = hourly * 40,
            MonthlyRate = hourly * 160,
            Amenities = amenities,
            TimeZoneId = "UTC"
        };
        db.ParkingSpaces.Add(space);
        return space.Id;
    }

    /// <summary>
    /// Point just outside the 100 m circle around the canonical center and just inside
    /// the 100 m circle around the raw center. Only canonical center plus canonical radius excludes it.
    /// </summary>
    private async Task<(double Lat, double Lng)> ProjectBeyondCanonicalCenterAsync(
        double canonicalLat,
        double canonicalLng,
        double rawLat,
        double rawLng)
    {
        const string sql = """
            WITH c AS (
                SELECT ST_SetSRID(ST_MakePoint(@clng, @clat), 4326)::geography AS g
            ),
            r AS (
                SELECT ST_SetSRID(ST_MakePoint(@rlng, @rlat), 4326)::geography AS g
            ),
            d AS (
                SELECT c.g AS cg, ST_Distance(c.g, r.g) AS meters, ST_Azimuth(c.g, r.g) AS azimuth
                FROM c, r
            )
            SELECT ST_Y(ST_Project(cg, meters + 99.2, azimuth)::geometry),
                   ST_X(ST_Project(cg, meters + 99.2, azimuth)::geometry)
            FROM d
            """;

        await using var conn = new NpgsqlConnection(_postgres.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("clat", canonicalLat);
        cmd.Parameters.AddWithValue("clng", canonicalLng);
        cmd.Parameters.AddWithValue("rlat", rawLat);
        cmd.Parameters.AddWithValue("rlng", rawLng);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetDouble(0), reader.GetDouble(1));
    }

    private async Task<(double Lat, double Lng)> ProjectNorthAsync(double lat, double lng, double meters)
    {
        const string sql = """
            SELECT ST_Y(ST_Project(ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography, @meters, 0)::geometry),
                   ST_X(ST_Project(ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography, @meters, 0)::geometry)
            """;

        await using var conn = new NpgsqlConnection(_postgres.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("lat", lat);
        cmd.Parameters.AddWithValue("lng", lng);
        cmd.Parameters.AddWithValue("meters", meters);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetDouble(0), reader.GetDouble(1));
    }

    private async Task<double> DistanceMetersAsync(Guid id, double lat, double lng)
    {
        const string sql = """
            SELECT ST_Distance(
                "Location",
                ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)::geography)
            FROM "ParkingSpaces"
            WHERE "Id" = @id
            """;

        await using var conn = new NpgsqlConnection(_postgres.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("lat", lat);
        cmd.Parameters.AddWithValue("lng", lng);
        var result = await cmd.ExecuteScalarAsync();
        result.Should().NotBeNull();
        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }
}
