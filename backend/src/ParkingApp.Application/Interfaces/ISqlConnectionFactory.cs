using System.Data;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Factory for creating raw database connections (used by Dapper query handlers).
/// This keeps read-side queries independent of EF Core for better performance.
/// </summary>
public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
