using FluentAssertions;
using Npgsql;
using ParkingApp.Infrastructure.Data;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Data;

public class NpgsqlConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_ReturnsNpgsqlConnectionWithCorrectConnectionString()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=test;Username=postgres;Password=admin";
        var factory = new NpgsqlConnectionFactory(connectionString);

        // Act
        var connection = factory.CreateConnection();

        // Assert
        connection.Should().BeOfType<NpgsqlConnection>();
        ((NpgsqlConnection)connection).ConnectionString.Should().Contain("Host=localhost");
    }
}
