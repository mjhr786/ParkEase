using System.Data;
using Dapper;
using FluentAssertions;
using Moq;
using Moq.Dapper;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class DashboardRepositoryTests
{
    private readonly Mock<ISqlConnectionFactory> _connectionFactoryMock;
    private readonly Mock<IDbConnection> _connectionMock;
    private readonly DashboardRepository _repository;

    public DashboardRepositoryTests()
    {
        _connectionFactoryMock = new Mock<ISqlConnectionFactory>();
        _connectionMock = new Mock<IDbConnection>();
        _connectionFactoryMock.Setup(f => f.CreateConnection()).Returns(_connectionMock.Object);
        _repository = new DashboardRepository(_connectionFactoryMock.Object);
    }

    [Fact]
    public async Task GetVendorAggregatesAsync_ReturnsData()
    {
        // Arrange
        var vendorId = Guid.NewGuid();
        var row = new VendorAggregateRow { TotalParkingSpaces = 5, TotalEarnings = 1000 };
        
        _connectionMock.SetupDapperAsync(c => c.QueryFirstOrDefaultAsync<VendorAggregateRow>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(row);

        // Act
        var result = await _repository.GetVendorAggregatesAsync(vendorId, DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        result.TotalParkingSpaces.Should().Be(5);
        result.TotalEarnings.Should().Be(1000);
    }

    [Fact]
    public async Task GetChartDataAsync_ReturnsList()
    {
        // Arrange
        var list = new List<DashboardChartDataDto> { new DashboardChartDataDto { Label = "Mon", Earnings = 100 } };
        _connectionMock.SetupDapperAsync(c => c.QueryAsync<DashboardChartDataDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(list);

        // Act
        var result = await _repository.GetChartDataAsync(Guid.NewGuid());

        // Assert
        result.Should().HaveCount(1);
        result[0].Label.Should().Be("Mon");
    }

    [Fact]
    public async Task GetRecentVendorBookingsAsync_ReturnsList()
    {
        // Arrange
        var list = new List<BookingDto> { new BookingDto { Id = Guid.NewGuid(), UserName = "Test" } };
        _connectionMock.SetupDapperAsync(c => c.QueryAsync<BookingDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(list);

        // Act
        var result = await _repository.GetRecentVendorBookingsAsync(Guid.NewGuid());

        // Assert
        result.Should().HaveCount(1);
        result[0].UserName.Should().Be("Test");
    }

    [Fact]
    public async Task GetMemberAggregatesAsync_ReturnsData()
    {
        // Arrange
        var row = new MemberAggregateRow { TotalBookings = 10 };
        _connectionMock.SetupDapperAsync(c => c.QueryFirstOrDefaultAsync<MemberAggregateRow>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(row);

        // Act
        var result = await _repository.GetMemberAggregatesAsync(Guid.NewGuid());

        // Assert
        result.TotalBookings.Should().Be(10);
    }

    [Fact]
    public async Task GetUpcomingMemberBookingsAsync_ReturnsList()
    {
        // Arrange
        var list = new List<BookingDto> { new BookingDto { Id = Guid.NewGuid() } };
        _connectionMock.SetupDapperAsync(c => c.QueryAsync<BookingDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(list);

        // Act
        var result = await _repository.GetUpcomingMemberBookingsAsync(Guid.NewGuid(), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecentMemberBookingsAsync_ReturnsList()
    {
        // Arrange
        var list = new List<BookingDto> { new BookingDto { Id = Guid.NewGuid() } };
        _connectionMock.SetupDapperAsync(c => c.QueryAsync<BookingDto>(It.IsAny<string>(), It.IsAny<object>(), null, null, null))
            .ReturnsAsync(list);

        // Act
        var result = await _repository.GetRecentMemberBookingsAsync(Guid.NewGuid());

        // Assert
        result.Should().HaveCount(1);
    }
}
