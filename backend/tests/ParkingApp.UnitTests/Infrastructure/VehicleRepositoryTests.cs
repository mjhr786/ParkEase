using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class VehicleRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly VehicleRepository _repository;

    public VehicleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new VehicleRepository(_context);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsUserVehiclesInOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var v1 = new Vehicle { Id = Guid.NewGuid(), UserId = userId, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "White", IsDefault = false, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var v2 = new Vehicle { Id = Guid.NewGuid(), UserId = userId, LicensePlate = "XYZ789", Make = "Honda", Model = "Civic", Color = "Black", IsDefault = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        
        _context.Vehicles.AddRange(v1, v2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.First().IsDefault.Should().BeTrue();
        result.Last().IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task GetDefaultVehicleAsync_ReturnsDefaultVehicle()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var v1 = new Vehicle { Id = Guid.NewGuid(), UserId = userId, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "White", IsDefault = true };
        
        _context.Vehicles.Add(v1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetDefaultVehicleAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(v1.Id);
    }
}
