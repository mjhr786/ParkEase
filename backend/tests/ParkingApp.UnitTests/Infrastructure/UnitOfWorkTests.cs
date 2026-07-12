using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Identity;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class UnitOfWorkTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IOutboxProcessor> _processorMock;

    public UnitOfWorkTests()
    {
        _processorMock = new Mock<IOutboxProcessor>();
        _processorMock
            .Setup(p => p.ProcessPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);
    }

    private UnitOfWork CreateUow()
    {
        var writer = new OutboxWriter(_context);
        return new UnitOfWork(_context, writer, _processorMock.Object, NullLogger<UnitOfWork>.Instance);
    }

    [Fact]
    public void Repositories_AreLazilyInitialized()
    {
        var uow = CreateUow();

        uow.Users.Should().NotBeNull();
        uow.ParkingSpaces.Should().NotBeNull();
        uow.Bookings.Should().NotBeNull();
        uow.Payments.Should().NotBeNull();
        uow.Reviews.Should().NotBeNull();
        uow.Conversations.Should().NotBeNull();
        uow.ChatMessages.Should().NotBeNull();
        uow.Favorites.Should().NotBeNull();
        uow.Notifications.Should().NotBeNull();
        uow.Vehicles.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_EnqueuesDomainEventsToOutbox_AndProcesses()
    {
        var uow = CreateUow();
        var user = User.Register("test@example.com", "hash", "Test", "User", "1234567890");
        user.AddDomainEvent(new BookingCancelledEvent(
            Guid.NewGuid(), user.Id, Guid.NewGuid(), "BK-1", "reason"));

        _context.Users.Add(user);

        await uow.SaveChangesAsync();

        _context.OutboxMessages.Should().ContainSingle();
        var msg = _context.OutboxMessages.Single();
        msg.TypeName.Should().Contain(nameof(BookingCancelledEvent));
        msg.IdempotencyKey.Should().StartWith(nameof(BookingCancelledEvent));
        user.DomainEvents.Should().BeEmpty();

        _processorMock.Verify(
            p => p.ProcessPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenProcessorFails_DoesNotThrow_AndKeepsOutboxRow()
    {
        _processorMock
            .Setup(p => p.ProcessPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler boom"));

        var uow = CreateUow();
        var user = User.Register("fail@example.com", "hash", "F", "U", "1");
        user.AddDomainEvent(new BookingConfirmedEvent(Guid.NewGuid(), user.Id, Guid.NewGuid(), "BK"));
        _context.Users.Add(user);

        var act = () => uow.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        _context.OutboxMessages.Should().ContainSingle(m => m.Status == OutboxStatus.Pending);
    }

    [Fact]
    public async Task TransactionMethods_Work()
    {
        var uow = CreateUow();

        await FluentActions.Invoking(() => uow.BeginTransactionAsync()).Should().NotThrowAsync();
        await FluentActions.Invoking(() => uow.CommitTransactionAsync()).Should().NotThrowAsync();
        await FluentActions.Invoking(() => uow.RollbackTransactionAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_DisposesContext()
    {
        var uow = CreateUow();
        uow.Dispose();

        var action = () => _context.Users.ToList();
        action.Should().Throw<ObjectDisposedException>();
    }
}
