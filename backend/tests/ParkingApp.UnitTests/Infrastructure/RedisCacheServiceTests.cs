using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Infrastructure.Caching;
using ParkingApp.Infrastructure.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        var testObj = new TestCacheObject { Name = "Test" };
        var payload = FramePlain(JsonSerializer.SerializeToUtf8Bytes(testObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));

        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_key1", CommandFlags.None))
            .ReturnsAsync((RedisValue)payload);

        var result = await _service.GetAsync<TestCacheObject>("key1");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_LegacyUnframedJson_StillDeserializes()
    {
        var testObj = new TestCacheObject { Name = "Legacy" };
        var json = JsonSerializer.SerializeToUtf8Bytes(testObj);

        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_legacy", CommandFlags.None))
            .ReturnsAsync((RedisValue)json);

        var result = await _service.GetAsync<TestCacheObject>("legacy");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Legacy");
    }

    [Fact]
    public async Task GetAsync_KeyDoesNotExist_ReturnsDefault()
    {
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_key_miss", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        var result = await _service.GetAsync<TestCacheObject>("key_miss");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ThrowsException_LogsAndReturnsDefault()
    {
        _databaseMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new Exception("Redis error"));

        var result = await _service.GetAsync<TestCacheObject>("err_key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithExpiry_UsesSingleStringSetWithTtl()
    {
        var testObj = new TestCacheObject { Name = "Test" };
        var expiry = TimeSpan.FromMinutes(5);

        await _service.SetAsync("key2", testObj, expiry);

        // StackExchange.Redis binds TimeSpan to Expiration (EX) — single round-trip, no KeyExpire.
        _databaseMock.Verify(d => d.StringSetAsync(
            (RedisKey)"TestPrefix_key2",
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            CommandFlags.None), Times.Once);

        _databaseMock.Verify(d => d.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<ExpireWhen>(),
            CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WithoutExpiry_AppliesDefaultTtl()
    {
        var testObj = new TestCacheObject { Name = "Test" };

        await _service.SetAsync("key3", testObj);

        _databaseMock.Verify(d => d.StringSetAsync(
            (RedisKey)"TestPrefix_key3",
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueIfKeyExists()
    {
        _databaseMock.Setup(d => d.KeyExistsAsync("TestPrefix_key4", CommandFlags.None)).ReturnsAsync(true);

        var result = await _service.ExistsAsync("key4");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_DeletesKey()
    {
        await _service.RemoveAsync("key5");

        _databaseMock.Verify(d => d.KeyDeleteAsync("TestPrefix_key5", CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_IncrementsAndSetsExpiry()
    {
        _databaseMock.Setup(d => d.StringIncrementAsync("TestPrefix_ctr", 1, CommandFlags.None)).ReturnsAsync(1L);

        var result = await _service.IncrementAsync("ctr", TimeSpan.FromMinutes(1));

        result.Should().Be(1L);
        _databaseMock.Verify(d => d.KeyExpireAsync(
            "TestPrefix_ctr",
            TimeSpan.FromMinutes(1),
            ExpireWhen.Always,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_WithoutExpiry_IncrementsOnly()
    {
        _databaseMock.Setup(d => d.StringIncrementAsync("TestPrefix_ctr2", 1, CommandFlags.None)).ReturnsAsync(2L);

        var result = await _service.IncrementAsync("ctr2");

        result.Should().Be(2L);
        _databaseMock.Verify(d => d.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<ExpireWhen>(),
            CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task RemoveByPatternAsync_VersionedNamespace_BumpsVersionInsteadOfKeysScan()
    {
        _databaseMock.Setup(d => d.StringIncrementAsync("TestPrefix_ver:search", 1, CommandFlags.None))
            .ReturnsAsync(3L);
        _databaseMock.Setup(d => d.KeyExpireAsync(
                "TestPrefix_ver:search",
                It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(),
                CommandFlags.None))
            .ReturnsAsync(true);

        await _service.RemoveByPatternAsync("search:*");

        _databaseMock.Verify(d => d.StringIncrementAsync("TestPrefix_ver:search", 1, CommandFlags.None), Times.Once);
        _redisMock.Verify(r => r.GetEndPoints(It.IsAny<bool>()), Times.Never);
        _databaseMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Never);
    }

    [Fact]
    public async Task GetAsync_VersionedNamespace_UsesVersionedPhysicalKey()
    {
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_ver:search", CommandFlags.None))
            .ReturnsAsync((RedisValue)"2");
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_search:v2:state:city", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        var result = await _service.GetAsync<TestCacheObject>("search:state:city");

        result.Should().BeNull();
        _databaseMock.Verify(d => d.StringGetAsync("TestPrefix_search:v2:state:city", CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsCachedValue()
    {
        var testObj = new TestCacheObject { Name = "Hit" };
        var payload = FramePlain(JsonSerializer.SerializeToUtf8Bytes(testObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_hit_key", CommandFlags.None))
            .ReturnsAsync((RedisValue)payload);

        var result = await _service.GetOrSetAsync("hit_key", () => Task.FromResult(new TestCacheObject { Name = "Factory" }));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Hit");
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactoryAndSets()
    {
        _databaseMock.Setup(d => d.StringGetAsync("TestPrefix_miss_key", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);

        var result = await _service.GetOrSetAsync("miss_key", () => Task.FromResult(new TestCacheObject { Name = "Factory" }));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Factory");
        _databaseMock.Verify(d => d.StringSetAsync(
            (RedisKey)"TestPrefix_miss_key",
            It.IsAny<RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task AcquireLockAsync_UsesSetWhenNotExists()
    {
        // Cover both classic (TimeSpan?/When) and newer (Expiration/ValueCondition) overloads.
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var acquired = await _service.AcquireLockAsync("resource", TimeSpan.FromSeconds(10));

        acquired.Should().BeTrue();
        _databaseMock.Invocations
            .Count(i => i.Method.Name == "StringSetAsync"
                        && i.Arguments.Count > 0
                        && i.Arguments[0]!.ToString() == "TestPrefix_lock:resource")
            .Should().Be(1);
    }

    [Fact]
    public void RedisConnectionFactory_IsConfigured_RejectsPlaceholders()
    {
        RedisConnectionFactory.IsConfigured(null).Should().BeFalse();
        RedisConnectionFactory.IsConfigured("").Should().BeFalse();
        RedisConnectionFactory.IsConfigured("localhost:6379").Should().BeFalse();
        RedisConnectionFactory.IsConfigured("SET_VIA_USER_SECRETS_OR_ENV_VAR").Should().BeFalse();
        RedisConnectionFactory.IsConfigured("rediss://default:pwd@host.upstash.io:6379").Should().BeTrue();
        RedisConnectionFactory.IsConfigured("localhost:6380").Should().BeTrue();
        RedisConnectionFactory.IsConfigured("localhost:6379,password=DevRedis@123,abortConnect=false")
            .Should().BeTrue();
    }

    [Fact]
    public void RedisConnectionFactory_DescribeTarget_DistinguishesLocalAndUpstash()
    {
        RedisConnectionFactory.DescribeTarget(null).Should().Be("not configured");
        RedisConnectionFactory.DescribeTarget("localhost:6379,password=DevRedis@123")
            .Should().Be("local Docker");
        RedisConnectionFactory.DescribeTarget("rediss://default:pwd@host.upstash.io:6379")
            .Should().Be("Upstash");
    }

    [Fact]
    public void RedisConnectionFactory_CreateOptions_EnablesSslForUpstash()
    {
        var options = RedisConnectionFactory.CreateOptions(
            "rediss://default:pwd@upright-lark-103744.upstash.io:6379",
            new RedisCacheOptions());

        options.Ssl.Should().BeTrue();
        options.AbortOnConnectFail.Should().BeFalse();
        options.User.Should().Be("default");
        options.Password.Should().Be("pwd");
        options.EndPoints.Should().ContainSingle();
        options.EndPoints[0].ToString().Should().Contain("upright-lark-103744.upstash.io:6379");
    }

    [Fact]
    public void RedisConnectionFactory_CreateOptions_DisablesSslForLocalDocker()
    {
        var options = RedisConnectionFactory.CreateOptions(
            "localhost:6379,password=DevRedis@123,abortConnect=false",
            new RedisCacheOptions());

        options.Ssl.Should().BeFalse();
        options.AbortOnConnectFail.Should().BeFalse();
        options.Password.Should().Be("DevRedis@123");
        options.EndPoints.Should().ContainSingle();
        options.EndPoints[0].ToString().Should().Contain("localhost:6379");
    }

    [Fact]
    public void RedisConnectionFactory_ParseConnectionString_HandlesClassicCsv()
    {
        var options = RedisConnectionFactory.ParseConnectionString(
            "localhost:6380,password=secret,ssl=False,abortConnect=False");

        options.Password.Should().Be("secret");
        options.EndPoints.Should().ContainSingle();
    }

    private static byte[] FramePlain(byte[] json)
    {
        var result = new byte[json.Length + 1];
        result[0] = (byte)'0';
        Buffer.BlockCopy(json, 0, result, 1, json.Length);
        return result;
    }
}
