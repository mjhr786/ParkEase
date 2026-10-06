using FluentAssertions;
using ParkingApp.Application.Caching;

namespace ParkingApp.UnitTests.Caching;

public class CacheKeysSearchTests
{
    [Fact]
    public void Search_DifferentCoordinates_ProduceDifferentKeys()
    {
        var a = CacheKeys.Search(
            null, "Pune", null, null, null, null, null, "", 1, 12,
            latitude: 18.5204, longitude: 73.8567, radiusKm: 5);
        var b = CacheKeys.Search(
            null, "Pune", null, null, null, null, null, "", 1, 12,
            latitude: 19.0760, longitude: 72.8777, radiusKm: 5);

        a.Should().NotBe(b);
        a.Should().Contain("geo:");
        b.Should().Contain("geo:");
    }

    [Fact]
    public void Search_RoundsCoordinates_ForStableKeys()
    {
        var a = CacheKeys.Search(
            null, null, null, null, null, null, null, "", 1, 20,
            latitude: 18.52041, longitude: 73.85671, radiusKm: 5.04);
        var b = CacheKeys.Search(
            null, null, null, null, null, null, null, "", 1, 20,
            latitude: 18.52044, longitude: 73.85672, radiusKm: 5.0);

        // 4 decimal places / 1 decimal radius
        a.Should().Be(b);
        CacheKeys.RoundCoord(18.52041).Should().Be("18.5204");
        CacheKeys.RoundRadius(5.04).Should().Be("5.0");
    }

    [Fact]
    public void Map_RoundsCoordinates_LikeSearch()
    {
        var a = CacheKeys.Map(null, null, null, null, null, null, null, 3.0, 18.52, 73.8567, "");
        var b = CacheKeys.Map(null, null, null, null, null, null, null, 3.0, 18.52001, 73.85671, "");
        a.Should().Be(b);
    }

    [Fact]
    public void Search_OsrmFlag_ChangesCacheKey_WithoutAffectingDefaultCompatibility()
    {
        var withOsrm = CacheKeys.Search(
            null, null, null, null, null, null, null, "", 1, 12,
            latitude: 18.52, longitude: 73.85, useOsrmOnSearch: true);
        var withoutOsrm = CacheKeys.Search(
            null, null, null, null, null, null, null, "", 1, 12,
            latitude: 18.52, longitude: 73.85, useOsrmOnSearch: false);

        withOsrm.Should().NotBe(withoutOsrm);
        withOsrm.Should().Contain("osrm:1");
        withoutOsrm.Should().Contain("osrm:0");
    }

    [Fact]
    public void CanonicalCoordinate_MatchesRoundedKey_AndIsIdempotent()
    {
        var first = CacheKeys.CanonicalCoordinate(18.52041);
        var sameBucket = CacheKeys.CanonicalCoordinate(18.52044);
        var nextBucket = CacheKeys.CanonicalCoordinate(18.52045);

        first.Should().Be(sameBucket);
        CacheKeys.RoundCoord(first).Should().Be("18.5204");
        CacheKeys.RoundCoord(CacheKeys.CanonicalCoordinate(first)).Should().Be("18.5204");
        nextBucket.Should().NotBe(first);
        CacheKeys.RoundCoord(nextBucket).Should().Be("18.5205");
    }

    [Fact]
    public void CanonicalRadius_UsesExistingOneDecimalRounding()
    {
        CacheKeys.CanonicalRadius(5.04).Should().Be(CacheKeys.CanonicalRadius(5.0));
        CacheKeys.RoundRadius(CacheKeys.CanonicalRadius(5.04)).Should().Be("5.0");
        CacheKeys.CanonicalRadius(1.25).Should().Be(1.2);
        CacheKeys.CanonicalRadius(1.35).Should().Be(1.4);
        CacheKeys.RoundRadius(CacheKeys.CanonicalRadius(1.35)).Should().Be("1.4");
    }
}
