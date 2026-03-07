using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class UserRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new UserRepository(_context);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsUser()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByEmailAsync("TEST@test.com");
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByRefreshTokenAsync_ReturnsUser()
    {
        var user = new User { 
            Id = Guid.NewGuid(), Email = "test@test.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P",
            RefreshToken = "token", RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1)
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByRefreshTokenAsync("token");
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }
}
