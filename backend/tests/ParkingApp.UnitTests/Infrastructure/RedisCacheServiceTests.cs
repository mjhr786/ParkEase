using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Infrastructure.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class RedisCacheServiceTests
{
    private class TestCacheObject
    {
        public string Name { get; set; } = string.Empty;
    }

    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisCacheService>> _loggerMock;
    private readonly RedisCacheService _service;

    public RedisCacheServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisCacheService>>();
        
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
        
        _service = new RedisCacheService(_redisMock.Object, _loggerMock.Object, "TestPrefix_");
    }

    [Fact]
    public async Task GetAsync_KeyExists_ReturnsDeserializedObject()
    {
        // Arrange
        var testObj = new TestCacheObject { Name = "Test" };
        var json = JsonSerializer.Serialize(testObj);
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_key1", CommandFlags.None))
            .ReturnsAsync((RedisValue)json);

        // Act
        var result = await _service.GetAsync<TestCacheObject>("key1");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_KeyDoesNotExist_ReturnsDefault()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_key_miss", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetAsync<TestCacheObject>("key_miss");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ThrowsException_LogsAndReturnsDefault()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new Exception("Redis error"));

        // Act
        var result = await _service.GetAsync<TestCacheObject>("err_key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithExpiry_SetsValueAndExpiry()
    {
        // Arrange
        var testObj = new TestCacheObject { Name = "Test" };
        var expiry = TimeSpan.FromMinutes(5);

        // Act
        await _service.SetAsync("key2", testObj, expiry);

        // Assert
        // Verify expiry is set
        _databaseMock.Verify(d => d.KeyExpireAsync("TestPrefix_key2", expiry, ExpireWhen.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithoutExpiry_SetsValue()
    {
        // Arrange
        var testObj = new TestCacheObject { Name = "Test" };

        // Act
        await _service.SetAsync("key3", testObj);

        // Assert
        // Verify expiry is never set
        _databaseMock.Verify(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueIfKeyExists()
    {
        // Arrange
        _databaseMock.Setup(d => d.KeyExistsAsync("TestPrefix_key4", CommandFlags.None)).ReturnsAsync(true);

        // Act
        var result = await _service.ExistsAsync("key4");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        // Act
        await _service.RemoveAsync("key5");

        // Assert
        _databaseMock.Verify(d => d.KeyDeleteAsync("TestPrefix_key5", CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_IncrementsAndSetsExpiry()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringIncrementAsync("TestPrefix_ctr", 1, CommandFlags.None)).ReturnsAsync(1L);

        // Act
        var result = await _service.IncrementAsync("ctr", TimeSpan.FromMinutes(1));

        // Assert
        result.Should().Be(1L);
        _databaseMock.Verify(d => d.KeyExpireAsync("TestPrefix_ctr", TimeSpan.FromMinutes(1), ExpireWhen.Always, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_WithoutExpiry_IncrementsOnly()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringIncrementAsync("TestPrefix_ctr2", 1, CommandFlags.None)).ReturnsAsync(2L);

        // Act
        var result = await _service.IncrementAsync("ctr2");

        // Assert
        result.Should().Be(2L);
        _databaseMock.Verify(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<ExpireWhen>(), CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task RemoveByPatternAsync_CallsServerKeysAndDeletes()
    {
        // Arrange
        var serverMock = new Mock<IServer>();
        var endpointMock = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379);
        _redisMock.Setup(r => r.GetEndPoints(false)).Returns(new System.Net.EndPoint[] { endpointMock });
        _redisMock.Setup(r => r.GetServer(endpointMock, null)).Returns(serverMock.Object);

        var keys = new[] { (RedisKey)"TestPrefix_pattern_1", (RedisKey)"TestPrefix_pattern_2" };
        serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), "TestPrefix_pattern*", 250, 0, 0, CommandFlags.None))
                  .Returns(ToAsyncEnumerable(keys));

        // Act
        await _service.RemoveByPatternAsync("pattern*");

        // Assert
        _databaseMock.Verify(d => d.KeyDeleteAsync("TestPrefix_pattern_1", CommandFlags.None), Times.Once);
        _databaseMock.Verify(d => d.KeyDeleteAsync("TestPrefix_pattern_2", CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        var testObj = new TestCacheObject { Name = "Hit" };
        var json = JsonSerializer.Serialize(testObj);
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_hit_key", CommandFlags.None))
            .ReturnsAsync((RedisValue)json);

        // Act
        var result = await _service.GetOrSetAsync("hit_key", () => Task.FromResult(new TestCacheObject { Name = "Factory" }));

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Hit");
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactoryAndSets()
    {
        // Arrange
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_miss_key", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetOrSetAsync("miss_key", () => Task.FromResult(new TestCacheObject { Name = "Factory" }));

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Factory");

    }

    private async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(IEnumerable<RedisKey> keys)
    {
        foreach (var key in keys)
        {
            yield return key;
        }
        await Task.CompletedTask;
    }
}
