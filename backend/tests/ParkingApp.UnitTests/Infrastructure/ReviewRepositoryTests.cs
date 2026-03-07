using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class ReviewRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly ReviewRepository _repository;

    public ReviewRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new ReviewRepository(_context);
    }

    private (User, ParkingSpace) CreateBaseEntities()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"u{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "S", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = Guid.NewGuid() };
        _context.Users.Add(user);
        _context.ParkingSpaces.Add(space);
        return (user, space);
    }

    [Fact]
    public async Task GetByParkingSpaceIdAsync_ReturnsReviews()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        _context.Reviews.Add(new Review { Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, Rating = 5, Comment = "Great" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByParkingSpaceIdAsync(space.Id);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAverageRatingAsync_CalculatesCorrectly()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        _context.Reviews.Add(new Review { Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, Rating = 5, Comment = "G" });
        _context.Reviews.Add(new Review { Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, Rating = 1, Comment = "B" });
        await _context.SaveChangesAsync();

        // Act
        var avg = await _repository.GetAverageRatingAsync(space.Id);

        // Assert
        avg.Should().Be(3);
    }
}
