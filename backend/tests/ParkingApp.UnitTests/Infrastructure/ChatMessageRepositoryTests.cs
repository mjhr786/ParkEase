using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class ChatMessageRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly ChatMessageRepository _repository;

    public ChatMessageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new ChatMessageRepository(_context);
    }

    private (Conversation, User, User) CreateBaseEntities()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"u{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var vendor = new User { Id = Guid.NewGuid(), Email = $"v{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "S", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = vendor.Id };
        var convo = new Conversation { Id = Guid.NewGuid(), UserId = user.Id, VendorId = vendor.Id, ParkingSpaceId = space.Id };
        
        _context.Users.AddRange(user, vendor);
        _context.ParkingSpaces.Add(space);
        _context.Conversations.Add(convo);
        
        return (convo, user, vendor);
    }

    [Fact]
    public async Task GetByConversationIdAsync_ReturnsMessages()
    {
        // Arrange
        var (convo, user, vendor) = CreateBaseEntities();
        _context.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = convo.Id, SenderId = user.Id, Content = "Hello" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByConversationIdAsync(convo.Id);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        // Arrange
        var (convo, user, vendor) = CreateBaseEntities();
        _context.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = convo.Id, SenderId = vendor.Id, Content = "Hi", IsRead = false });
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetUnreadCountAsync(user.Id);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesMessages()
    {
        // Arrange
        var (convo, user, vendor) = CreateBaseEntities();
        var m1 = new ChatMessage { Id = Guid.NewGuid(), ConversationId = convo.Id, SenderId = vendor.Id, Content = "M1", IsRead = false };
        _context.ChatMessages.Add(m1);
        await _context.SaveChangesAsync();

        // Act
        await _repository.MarkAsReadAsync(convo.Id, user.Id);
        await _context.SaveChangesAsync();

        // Assert
        var m = await _context.ChatMessages.IgnoreQueryFilters().FirstAsync(x => x.Id == m1.Id);
        m.IsRead.Should().BeTrue();
    }
}
