using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class OutboxProcessorTests
{
    private sealed class CountingHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated notification failure");
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ProcessPending_OnHandlerSuccess_MarksProcessed()
    {
        await using var db = CreateDb();
        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        var sp = services.BuildServiceProvider();

        var writer = new OutboxWriter(db);
        var evt = new BookingCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BK", "r");
        writer.Enqueue(evt);
        await db.SaveChangesAsync();

        var processor = new OutboxProcessor(db, sp, NullLogger<OutboxProcessor>.Instance);
        var n = await processor.ProcessPendingAsync();

        n.Should().Be(1);
        handler.Calls.Should().Be(1);
        db.OutboxMessages.Single().Status.Should().Be(OutboxStatus.Processed);
    }

    [Fact]
    public async Task ProcessPending_OnHandlerFailure_KeepsMessageForRetry()
    {
        await using var db = CreateDb();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(new FailingHandler());
        var sp = services.BuildServiceProvider();

        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BK", "r"));
        await db.SaveChangesAsync();

        var processor = new OutboxProcessor(db, sp, NullLogger<OutboxProcessor>.Instance);
        var n = await processor.ProcessPendingAsync();

        n.Should().Be(0);
        var msg = db.OutboxMessages.Single();
        msg.Status.Should().Be(OutboxStatus.Pending);
        msg.AttemptCount.Should().Be(1);
        msg.LastError.Should().Contain("simulated");
        msg.AvailableAfterUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPending_SecondSuccessfulRun_DoesNotDuplicateHandlerCalls_WhenAlreadyProcessed()
    {
        await using var db = CreateDb();
        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        var sp = services.BuildServiceProvider();

        var bookingId = Guid.NewGuid();
        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingCancelledEvent(bookingId, Guid.NewGuid(), Guid.NewGuid(), "BK", "r"));
        await db.SaveChangesAsync();

        var processor = new OutboxProcessor(db, sp, NullLogger<OutboxProcessor>.Instance);
        await processor.ProcessPendingAsync();
        handler.Calls.Should().Be(1);

        // Simulate retry of already-processed idempotency key (duplicate enqueue prevented by business, but status guard)
        var processed = db.OutboxMessages.Single();
        processed.Status.Should().Be(OutboxStatus.Processed);

        // Re-process batch should not re-invoke handler for processed rows
        await processor.ProcessPendingAsync();
        handler.Calls.Should().Be(1);
    }
}
