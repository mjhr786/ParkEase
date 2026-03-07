using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class ParkingSpaceRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly ParkingSpaceRepository _repository;

    public ParkingSpaceRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new ParkingSpaceRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesOwner()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = "owner@test.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "Space", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = owner.Id };
        _context.Users.Add(owner);
        _context.ParkingSpaces.Add(space);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(space.Id);
        result.Should().NotBeNull();
        result!.Owner.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_FiltersByStateAndCity()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = "o@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var s1 = new ParkingSpace { Id = Guid.NewGuid(), Title = "S1", Description = "D", Address = "A", City = "New York", State = "NY", Country = "USA", PostalCode = "10001", OwnerId = owner.Id, IsActive = true };
        var s2 = new ParkingSpace { Id = Guid.NewGuid(), Title = "S2", Description = "D", Address = "A", City = "Los Angeles", State = "CA", Country = "USA", PostalCode = "90001", OwnerId = owner.Id, IsActive = true };
        _context.Users.Add(owner);
        _context.ParkingSpaces.AddRange(s1, s2);
        await _context.SaveChangesAsync();

        var result = await _repository.SearchAsync(state: "NY", city: "York");
        result.Should().HaveCount(1);
        result.First().City.Should().Be("New York");
    }

    [Fact]
    public async Task GetMapCoordinatesAsync_ReturnsModels()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = "o@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { 
            Id = Guid.NewGuid(), Title = "Space", Description = "D", Address = "Add", 
            City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = owner.Id, IsActive = true,
            Latitude = 10, Longitude = 20
        };
        _context.Users.Add(owner);
        _context.ParkingSpaces.Add(space);
        await _context.SaveChangesAsync();

        var result = await _repository.GetMapCoordinatesAsync();
        result.Should().HaveCount(1);
        result.First().Latitude.Should().Be(10);
    }
}
