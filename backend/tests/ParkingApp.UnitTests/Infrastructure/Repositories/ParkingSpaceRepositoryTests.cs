using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Marketplace.Infrastructure.Repositories;
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

    private User AddOwner()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = "owner@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P", IsActive = true };
        _context.Users.Add(owner);
        return owner;
    }

    [Fact]
    public async Task GetByIdAsync_IncludesOwner()
    {
        var owner = AddOwner();
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "Space", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = owner.Id };
        _context.ParkingSpaces.Add(space);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(space.Id);
        result.Should().NotBeNull();
        result!.OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByStateAndCity()
    {
        var owner = AddOwner();
        var s1 = new ParkingSpace { Id = Guid.NewGuid(), Title = "S1", Description = "D", Address = "A", City = "New York", State = "NY", Country = "USA", PostalCode = "10001", OwnerId = owner.Id, IsActive = true };
        var s2 = new ParkingSpace { Id = Guid.NewGuid(), Title = "S2", Description = "D", Address = "A", City = "Los Angeles", State = "CA", Country = "USA", PostalCode = "90001", OwnerId = owner.Id, IsActive = true };
        _context.ParkingSpaces.AddRange(s1, s2);
        await _context.SaveChangesAsync();

        var result = await _repository.SearchAsync(state: "NY", city: "York");
        result.Should().HaveCount(1);
        result.First().City.Should().Be("New York");
    }

    [Fact]
    public async Task GetMapCoordinatesAsync_ReturnsModels()
    {
        var owner = AddOwner();
        var space = new ParkingSpace {
            Id = Guid.NewGuid(), Title = "Space", Description = "D", Address = "Add",
            City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = owner.Id, IsActive = true,
            Latitude = 10, Longitude = 20
        };
        _context.ParkingSpaces.Add(space);
        await _context.SaveChangesAsync();

        var result = await _repository.GetMapCoordinatesAsync();
        result.Should().HaveCount(1);
        result.First().Latitude.Should().Be(10);
    }

    [Fact]
    public async Task ExistsWithZoneCodeAsync_MatchesExactZone()
    {
        var owner = AddOwner();
        var space = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            Title = "Space",
            Description = "D",
            Address = "A",
            City = "C",
            State = "S",
            Country = "Cu",
            PostalCode = "P",
            OwnerId = owner.Id,
            ZoneCode = "ZONE-A"
        };
        _context.ParkingSpaces.Add(space);
        await _context.SaveChangesAsync();

        (await _repository.ExistsWithZoneCodeAsync("ZONE-A")).Should().BeTrue();
        (await _repository.ExistsWithZoneCodeAsync("ZONE-B")).Should().BeFalse();
        (await _repository.ExistsWithZoneCodeAsync("")).Should().BeFalse();
    }
}





