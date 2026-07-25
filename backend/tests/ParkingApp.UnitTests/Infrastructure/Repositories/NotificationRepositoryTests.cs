using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.Messaging.Infrastructure.Repositories;
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
        var otherUserId = Guid.NewGuid();
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T1", Message = "M1", IsRead = false });
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T2", Message = "M2", IsRead = false });
        // Already-read row: leave alone (still read after).
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "T3", Message = "M3", IsRead = true, ReadAt = DateTime.UtcNow.AddDays(-1) });
        // Other user must not be affected.
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Other", Message = "M", IsRead = false });
        await _context.SaveChangesAsync();

        await _repository.MarkAllAsReadAsync(userId);
        await _context.SaveChangesAsync();

        var unreadForUser = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        unreadForUser.Should().Be(0);

        var userRows = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
        userRows.Should().OnlyContain(n => n.IsRead);
        userRows.Where(n => n.Title is "T1" or "T2").Should().OnlyContain(n => n.ReadAt != null);

        var otherUnread = await _context.Notifications.CountAsync(n => n.UserId == otherUserId && !n.IsRead);
        otherUnread.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAllAsync_SoftDeletes_OnlyTargetUser()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "A", Message = "M", IsRead = false });
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Title = "B", Message = "M", IsRead = true });
        _context.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = otherUserId, Title = "C", Message = "M", IsRead = false });
        await _context.SaveChangesAsync();

        await _repository.DeleteAllAsync(userId);
        await _context.SaveChangesAsync();

        // Query filter hides soft-deleted rows for the user.
        var visibleForUser = await _context.Notifications.CountAsync(n => n.UserId == userId);
        visibleForUser.Should().Be(0);

        // Rows still exist physically with IsDeleted = true (not hard-deleted).
        var deletedFlags = await _context.Notifications
            .IgnoreQueryFilters()
            .Where(n => n.UserId == userId)
            .Select(n => n.IsDeleted)
            .ToListAsync();
        deletedFlags.Should().HaveCount(2);
        deletedFlags.Should().OnlyContain(d => d);

        // Other user's notifications untouched.
        var otherVisible = await _context.Notifications.CountAsync(n => n.UserId == otherUserId);
        otherVisible.Should().Be(1);
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





