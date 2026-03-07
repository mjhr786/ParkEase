using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class InMemoryCacheServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly InMemoryCacheService _cacheService;

    public InMemoryCacheServiceTests()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        _memoryCache = provider.GetRequiredService<IMemoryCache>();
        var logger = provider.GetRequiredService<ILogger<InMemoryCacheService>>();
        _cacheService = new InMemoryCacheService(_memoryCache, logger);
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_ShouldWork()
    {
        // Arrange
        string key = "test-key";
        string value = "test-value";

        // Act
        await _cacheService.SetAsync(key, value);
        var actValue = await _cacheService.GetAsync<string>(key);

        // Assert
        actValue.Should().Be(value);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyNotExists()
    {
        // Act
        var actValue = await _cacheService.GetAsync<string>("missing-key");

        // Assert
        actValue.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        string key = "exist-key";
        await _cacheService.SetAsync(key, "val");

        // Act
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyNotExists()
    {
        // Act
        var exists = await _cacheService.ExistsAsync("non-exist-key");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteKey()
    {
        // Arrange
        string key = "remove-key";
        await _cacheService.SetAsync(key, "data");

        // Act
        await _cacheService.RemoveAsync(key);
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveByPatternAsync_ShouldRemoveMatchingKeys()
    {
        // Arrange
        await _cacheService.SetAsync("user:1:profile", "data1");
        await _cacheService.SetAsync("user:2:profile", "data2");
        await _cacheService.SetAsync("settings:1", "data3");

        // Act
        await _cacheService.RemoveByPatternAsync("user:*:profile");

        // Assert
        (await _cacheService.ExistsAsync("user:1:profile")).Should().BeFalse();
        (await _cacheService.ExistsAsync("user:2:profile")).Should().BeFalse();
        (await _cacheService.ExistsAsync("settings:1")).Should().BeTrue();
    }

    [Fact]
    public async Task IncrementAsync_ShouldIncrementValue()
    {
        // Arrange
        string key = "counter-key";

        // Act
        var val1 = await _cacheService.IncrementAsync(key);
        var val2 = await _cacheService.IncrementAsync(key);

        // Assert
        val1.Should().Be(1);
        val2.Should().Be(2);
        
        var storedVal = await _cacheService.GetAsync<long>(key);
        storedVal.Should().Be(2);
    }
}
