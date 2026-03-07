using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Infrastructure.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithoutRedis_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?> {
            {"ConnectionStrings:DefaultConnection", "Host=localhost;Database=test;Username=postgres;Password=admin"},
            {"ConnectionStrings:Redis", ""},
            {"Jwt:SecretKey", "super_secret_key_for_testing"},
            {"Resend:ApiKey", "test_resend_api_key"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<ApplicationDbContext>().Should().NotBeNull();
        provider.GetService<ISqlConnectionFactory>().Should().BeOfType<NpgsqlConnectionFactory>();
        provider.GetService<IDomainEventDispatcher>().Should().BeOfType<DomainEventDispatcher>();
        provider.GetService<IUnitOfWork>().Should().BeOfType<UnitOfWork>();
        provider.GetService<ITokenService>().Should().BeOfType<JwtTokenService>();
        provider.GetService<IPaymentService>().Should().BeOfType<StripePaymentService>();
        provider.GetService<IEmailService>().Should().BeOfType<ResendEmailService>();
        provider.GetService<ICacheService>().Should().BeOfType<InMemoryCacheService>();
    }

    [Fact]
    public void AddInfrastructure_WithRedis_RegistersRedisServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?> {
            {"ConnectionStrings:DefaultConnection", "Host=localhost;Database=test;Username=postgres;Password=admin"},
            {"ConnectionStrings:Redis", "localhost:6380"},
            {"Jwt:SecretKey", "super_secret_key_for_testing"},
            {"Resend:ApiKey", "test_resend_api_key"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        // We can't strictly assert the resolution of ICacheService here because it attempts to connect to localhost:6380
        // and throws a RedisConnectionException during BuildServiceProvider/GetService, so we just check the descriptor instead.
        services.Should().Contain(d => d.ServiceType == typeof(ICacheService) && d.ImplementationFactory != null);
        services.Should().Contain(d => d.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer));
    }
}
