using Moq;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingApp.Application.CQRS.Queries.Vehicles;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using System;

namespace ParkingApp.UnitTests.Vehicles;

public class VehicleQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IVehicleRepository> _mockVehicleRepo;

    public VehicleQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockVehicleRepo = new Mock<IVehicleRepository>();
        _mockUow.Setup(u => u.Vehicles).Returns(_mockVehicleRepo.Object);
    }

    [Fact]
    public async Task GetMyVehicles_ShouldReturnVehicleDtos()
    {
        var handler = new GetMyVehiclesQueryHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        
        var vehicles = new List<Vehicle>
        {
            new Vehicle { Id = Guid.NewGuid(), UserId = userId, LicensePlate = "ABC", Make = "Toyota" },
            new Vehicle { Id = Guid.NewGuid(), UserId = userId, LicensePlate = "XYZ", Make = "Honda" }
        };

        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicles);

        var result = await handler.HandleAsync(new GetMyVehiclesQuery(userId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.Should().Contain(d => d.LicensePlate == "ABC");
        result.Data.Should().Contain(d => d.LicensePlate == "XYZ");
    }

    [Fact]
    public async Task GetMyVehicles_ShouldReturnEmptyList_WhenNoVehiclesExist()
    {
        var handler = new GetMyVehiclesQueryHandler(_mockUow.Object);
        var userId = Guid.NewGuid();

        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Vehicle>());

        var result = await handler.HandleAsync(new GetMyVehiclesQuery(userId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }
}
