using Testcontainers.PostgreSql;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// Shared Testcontainers Postgres for dual-pool SQL integration tests.
/// Requires Docker. Collection-scoped so one container serves all SQL ITs.
/// </summary>
public sealed class PostgresSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("parkease_dual_pool_it")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresSqlCollection : ICollectionFixture<PostgresSqlFixture>
{
    public const string Name = "PostgresSql";
}
