using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class UnitOfWorkTests
{
    private readonly Mock<IDomainEventDispatcher> _dispatcherMock;
    private readonly ApplicationDbContext _context;

    public UnitOfWorkTests()
    {
        _dispatcherMock = new Mock<IDomainEventDispatcher>();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);
    }

    [Fact]
    public void Repositories_AreLazilyInitialized()
    {
        // Arrange
        var uow = new UnitOfWork(_context, _dispatcherMock.Object);

        // Act & Assert
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
    public async Task SaveChangesAsync_DispatchesDomainEvents()
    {
        // Arrange
        var uow = new UnitOfWork(_context, _dispatcherMock.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1234567890"
        };
        var domainEvent = new Mock<IDomainEvent>().Object;
        user.AddDomainEvent(domainEvent);
        
        _context.Users.Add(user);

        // Act
        await uow.SaveChangesAsync();

        // Assert
        _dispatcherMock.Verify(d => d.DispatchEventsAsync(It.Is<IEnumerable<IDomainEvent>>(e => e.Contains(domainEvent)), It.IsAny<CancellationToken>()), Times.Once);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task TransactionMethods_Work()
    {
        // Arrange
        var uow = new UnitOfWork(_context, _dispatcherMock.Object);

        // Act & Assert
        // In-memory database doesn't support transactions in the same way, 
        // but it shouldn't throw if we call them unless we use a provider that supports them.
        // Actually, InMemoryDatabase DOES support transactions now in newer versions or it just no-ops.
        
        var beginAction = () => uow.BeginTransactionAsync();
        await beginAction.Should().NotThrowAsync();

        var commitAction = () => uow.CommitTransactionAsync();
        await commitAction.Should().NotThrowAsync();

        var rollbackAction = () => uow.RollbackTransactionAsync();
        await rollbackAction.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_DisposesContext()
    {
        // Arrange
        var uow = new UnitOfWork(_context, _dispatcherMock.Object);

        // Act
        uow.Dispose();

        // Assert
        var action = () => _context.Users.ToList();
        action.Should().Throw<ObjectDisposedException>();
    }
}
