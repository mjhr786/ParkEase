using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class FavoriteRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly FavoriteRepository _repository;

    public FavoriteRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new FavoriteRepository(_context);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsUserFavorites()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "Space 1", Description = "Desc", Address = "Add", City = "City", State = "ST", Country = "USA", PostalCode = "12345", OwnerId = Guid.NewGuid() };
        var favorite = new Favorite { Id = Guid.NewGuid(), UserId = userId, ParkingSpaceId = space.Id };
        
        _context.ParkingSpaces.Add(space);
        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        result.First().ParkingSpaceId.Should().Be(space.Id);
        result.First().ParkingSpace.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByUserAndSpaceAsync_ReturnsFavorite()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var favorite = new Favorite { Id = Guid.NewGuid(), UserId = userId, ParkingSpaceId = spaceId };
        
        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserAndSpaceAsync(userId, spaceId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(favorite.Id);
    }
}
