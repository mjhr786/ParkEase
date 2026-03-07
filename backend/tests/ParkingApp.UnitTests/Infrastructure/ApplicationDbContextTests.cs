using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Data;

public class ApplicationDbContextTests
{
    private DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task SaveChangesAsync_SetsCreatedAt_ForNewEntities()
    {
        // Arrange
        using var context = new ApplicationDbContext(CreateOptions());
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1234567890"
        };

        // Act
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert
        user.CreatedAt.Should().NotBe(default);
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_SetsUpdatedAt_ForModifiedEntities()
    {
        // Arrange
        using var context = new ApplicationDbContext(CreateOptions());
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1234567890"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var initialCreatedAt = user.CreatedAt;

        // Act
        user.FirstName = "Updated";
        await context.SaveChangesAsync();

        // Assert
        user.CreatedAt.Should().Be(initialCreatedAt);
        user.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ModelCreating_DoesNotThrow()
    {
        // Arrange
        var options = CreateOptions();
        using var context = new ApplicationDbContext(options);

        // Act
        var action = () => { _ = context.Model.GetEntityTypes(); };

        // Assert
        action.Should().NotThrow();
    }
}
