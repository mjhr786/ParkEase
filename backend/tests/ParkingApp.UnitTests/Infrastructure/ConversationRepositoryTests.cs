using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class ConversationRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly ConversationRepository _repository;

    public ConversationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new ConversationRepository(_context);
    }

    private (User, User, ParkingSpace) CreateBaseEntities()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"u{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var vendor = new User { Id = Guid.NewGuid(), Email = $"v{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "S", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = vendor.Id };
        
        _context.Users.AddRange(user, vendor);
        _context.ParkingSpaces.Add(space);
        
        return (user, vendor, space);
    }

    [Fact]
    public async Task GetByParticipantsAsync_ReturnsConversation()
    {
        // Arrange
        var (user, vendor, space) = CreateBaseEntities();
        var convo = new Conversation { Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, VendorId = vendor.Id };
        _context.Conversations.Add(convo);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByParticipantsAsync(space.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(convo.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsUserAndVendorConversations()
    {
        // Arrange
        var (user, vendor, space1) = CreateBaseEntities();
        var otherUser = new User { Id = Guid.NewGuid(), Email = $"other{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space2 = new ParkingSpace { Id = Guid.NewGuid(), Title = "S2", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = vendor.Id };
        _context.Users.Add(otherUser);
        _context.ParkingSpaces.Add(space2);
        
        _context.Conversations.Add(new Conversation { Id = Guid.NewGuid(), UserId = user.Id, VendorId = vendor.Id, ParkingSpaceId = space1.Id });
        _context.Conversations.Add(new Conversation { Id = Guid.NewGuid(), UserId = otherUser.Id, VendorId = user.Id, ParkingSpaceId = space2.Id });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().HaveCount(2);
    }
}
