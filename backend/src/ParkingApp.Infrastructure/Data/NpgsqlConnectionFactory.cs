using System.Data;
using Npgsql;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Data;

/// <summary>
/// Creates raw Npgsql connections for Dapper read queries.
/// Connections are NOT pooled here — Npgsql handles pooling internally.
/// </summary>
public sealed class NpgsqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
