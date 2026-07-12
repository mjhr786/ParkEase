using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class OutboxAdminStoreTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus_AndReturnsSummary()
    {
        await using var db = CreateDb();
        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "r"));
        writer.Enqueue(new BookingConfirmedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B"));
        await db.SaveChangesAsync();

        // Mark one failed
        var first = db.OutboxMessages.First();
        first.Status = OutboxStatus.Failed;
        first.LastError = "boom";
        await db.SaveChangesAsync();

        var store = new OutboxAdminStore(db);
        var list = await store.ListAsync(OutboxMessageStatusDto.Failed, null, 1, 50);

        list.Items.Should().HaveCount(1);
        list.Items[0].LastError.Should().Be("boom");
        list.Summary.Failed.Should().Be(1);
        list.Summary.Pending.Should().Be(1);
        list.Summary.Total.Should().Be(2);
    }

    [Fact]
    public async Task RequeueAsync_FailedMessage_BecomesPendingImmediately()
    {
        await using var db = CreateDb();
        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A", "r"));
        await db.SaveChangesAsync();

        var msg = db.OutboxMessages.Single();
        msg.Status = OutboxStatus.Failed;
        msg.LastError = "err";
        msg.AvailableAfterUtc = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        var store = new OutboxAdminStore(db);
        var ok = await store.RequeueAsync(msg.Id);

        ok.Should().BeTrue();
        var reloaded = db.OutboxMessages.Single();
        reloaded.Status.Should().Be(OutboxStatus.Pending);
        reloaded.LastError.Should().BeNull();
        reloaded.AvailableAfterUtc.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task RequeueAsync_ProcessedMessage_ReturnsFalse()
    {
        await using var db = CreateDb();
        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingConfirmedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B"));
        await db.SaveChangesAsync();

        var msg = db.OutboxMessages.Single();
        msg.Status = OutboxStatus.Processed;
        await db.SaveChangesAsync();

        var store = new OutboxAdminStore(db);
        (await store.RequeueAsync(msg.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RequeueAllFailed_ResetsAllFailedRows()
    {
        await using var db = CreateDb();
        db.OutboxMessages.AddRange(
            new OutboxMessage { Status = OutboxStatus.Failed, TypeName = "T1", Payload = "{}", IdempotencyKey = "k1" },
            new OutboxMessage { Status = OutboxStatus.Failed, TypeName = "T2", Payload = "{}", IdempotencyKey = "k2" },
            new OutboxMessage { Status = OutboxStatus.Pending, TypeName = "T3", Payload = "{}", IdempotencyKey = "k3" });
        await db.SaveChangesAsync();

        var store = new OutboxAdminStore(db);
        var count = await store.RequeueAllFailedAsync();

        count.Should().Be(2);
        db.OutboxMessages.Count(m => m.Status == OutboxStatus.Failed).Should().Be(0);
        db.OutboxMessages.Count(m => m.Status == OutboxStatus.Pending).Should().Be(3);
    }
}
