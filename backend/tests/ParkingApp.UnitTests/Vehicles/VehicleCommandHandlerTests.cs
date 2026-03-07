using Moq;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ParkingApp.Application.CQRS.Commands.Vehicles;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using System;

namespace ParkingApp.UnitTests.Vehicles;

public class VehicleCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IVehicleRepository> _mockVehicleRepo;

    public VehicleCommandHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockVehicleRepo = new Mock<IVehicleRepository>();
        _mockUow.Setup(u => u.Vehicles).Returns(_mockVehicleRepo.Object);
    }

    [Fact]
    public async Task CreateVehicle_ShouldFail_WhenLicensePlateIsDuplicate()
    {
        var handler = new CreateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var dto = new CreateVehicleDto("DUPLICATE", "Make", "Model", "Color", VehicleType.Car, false);
        
        var existingVehicles = new List<Vehicle> { new Vehicle { LicensePlate = "duplicate" } };
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(existingVehicles);

        var result = await handler.HandleAsync(new CreateVehicleCommand(userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateVehicle_ShouldMakeItDefault_WhenItIsTheOnlyVehicle()
    {
        var handler = new CreateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var dto = new CreateVehicleDto("UNIQUE", "Make", "Model", "Color", VehicleType.Car, false);
        
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Vehicle>());

        var result = await handler.HandleAsync(new CreateVehicleCommand(userId, dto));

        result.Success.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
        _mockVehicleRepo.Verify(r => r.AddAsync(It.Is<Vehicle>(v => v.IsDefault == true), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateVehicle_ShouldUnsetOldDefault_WhenNewOneIsDefault()
    {
        var handler = new CreateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var dto = new CreateVehicleDto("UNIQUE", "Make", "Model", "Color", VehicleType.Car, true);
        
        var oldDefault = new Vehicle { Id = Guid.NewGuid(), IsDefault = true };
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Vehicle> { oldDefault });
        _mockVehicleRepo.Setup(r => r.GetDefaultVehicleAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(oldDefault);

        var result = await handler.HandleAsync(new CreateVehicleCommand(userId, dto));

        result.Success.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
        oldDefault.IsDefault.Should().BeFalse();
        _mockVehicleRepo.Verify(r => r.Update(oldDefault), Times.Once);
    }

    [Fact]
    public async Task UpdateVehicle_ShouldFail_WhenNotFound()
    {
        var handler = new UpdateVehicleCommandHandler(_mockUow.Object);
        var result = await handler.HandleAsync(new UpdateVehicleCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateVehicleDto("", "", "", "", VehicleType.Car, false)));
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Vehicle not found");
    }

    [Fact]
    public async Task UpdateVehicle_ShouldFail_WhenUnauthorized()
    {
        var handler = new UpdateVehicleCommandHandler(_mockUow.Object);
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = Guid.NewGuid() };
        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);

        var result = await handler.HandleAsync(new UpdateVehicleCommand(vehicleId, Guid.NewGuid(), new UpdateVehicleDto("", "", "", "", VehicleType.Car, false)));
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized to update this vehicle");
    }

    [Fact]
    public async Task UpdateVehicle_ShouldFail_WhenLicensePlateIsDuplicate()
    {
        var handler = new UpdateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = userId, LicensePlate = "OLD" };
        
        var existingVehicles = new List<Vehicle> { vehicle, new Vehicle { Id = Guid.NewGuid(), LicensePlate = "DUPLICATE" } };
        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(existingVehicles);

        var result = await handler.HandleAsync(new UpdateVehicleCommand(vehicleId, userId, new UpdateVehicleDto("duplicate", "Make", "Model", "Color", VehicleType.Car, false)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task UpdateVehicle_ShouldForceDefault_WhenTryingToUnsetOnlyVehicle()
    {
        var handler = new UpdateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = userId, LicensePlate = "OLD", IsDefault = true };
        
        var existingVehicles = new List<Vehicle> { vehicle };
        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(existingVehicles);

        var result = await handler.HandleAsync(new UpdateVehicleCommand(vehicleId, userId, new UpdateVehicleDto("NEW", "Make", "Model", "Color", VehicleType.Car, false)));

        result.Success.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
        vehicle.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateVehicle_ShouldUnsetOldDefault_WhenSetToDefault()
    {
        var handler = new UpdateVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = userId, LicensePlate = "OLD", IsDefault = false };
        var oldDefault = new Vehicle { Id = Guid.NewGuid(), UserId = userId, IsDefault = true };

        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Vehicle> { vehicle, oldDefault });
        _mockVehicleRepo.Setup(r => r.GetDefaultVehicleAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(oldDefault);

        var result = await handler.HandleAsync(new UpdateVehicleCommand(vehicleId, userId, new UpdateVehicleDto("NEW", "Make", "Model", "Color", VehicleType.Car, true)));

        result.Success.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
        oldDefault.IsDefault.Should().BeFalse();
        _mockVehicleRepo.Verify(r => r.Update(oldDefault), Times.Once);
        _mockVehicleRepo.Verify(r => r.Update(vehicle), Times.Once);
    }

    [Fact]
    public async Task DeleteVehicle_ShouldFail_WhenNotFound()
    {
        var handler = new DeleteVehicleCommandHandler(_mockUow.Object);
        var result = await handler.HandleAsync(new DeleteVehicleCommand(Guid.NewGuid(), Guid.NewGuid()));
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVehicle_ShouldFail_WhenUnauthorized()
    {
        var handler = new DeleteVehicleCommandHandler(_mockUow.Object);
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = Guid.NewGuid() };
        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);

        var result = await handler.HandleAsync(new DeleteVehicleCommand(vehicleId, Guid.NewGuid()));
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVehicle_ShouldSetNewestAsDefault_WhenDeletingDefault()
    {
        var handler = new DeleteVehicleCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = vehicleId, UserId = userId, IsDefault = true };
        var newestRemaining = new Vehicle { Id = Guid.NewGuid(), UserId = userId, IsDefault = false };

        _mockVehicleRepo.Setup(r => r.GetByIdAsync(vehicleId, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        _mockVehicleRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Vehicle> { newestRemaining });

        var result = await handler.HandleAsync(new DeleteVehicleCommand(vehicleId, userId));

        result.Success.Should().BeTrue();
        newestRemaining.IsDefault.Should().BeTrue();
        _mockVehicleRepo.Verify(r => r.Remove(vehicle), Times.Once);
        _mockVehicleRepo.Verify(r => r.Update(newestRemaining), Times.Once);
    }
}
