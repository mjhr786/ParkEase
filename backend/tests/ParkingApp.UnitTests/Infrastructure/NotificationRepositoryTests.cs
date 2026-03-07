using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class NotificationRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationRepository _repository;

    public NotificationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new NotificationRepository(_context);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        var userId = Guid.NewGuid();
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T", Message = "M", IsRead = false });
        await _context.SaveChangesAsync();

        var count = await _repository.GetUnreadCountAsync(userId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_UpdatesAll()
    {
        var userId = Guid.NewGuid();
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T1", Message = "M1", IsRead = false });
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T2", Message = "M2", IsRead = false });
        await _context.SaveChangesAsync();

        await _repository.MarkAllAsReadAsync(userId);
        await _context.SaveChangesAsync();

        var unread = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        unread.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResults()
    {
        var userId = Guid.NewGuid();
        for (int i = 0; i < 15; i++)
        {
            _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = $"T{i}", Message = $"M{i}", IsRead = false });
        }
        await _context.SaveChangesAsync();

        var result = await _repository.GetPagedAsync(userId, 1, 10);
        result.Should().HaveCount(10);
    }
}
