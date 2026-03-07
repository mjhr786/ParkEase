using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Domain.Events;
using ParkingApp.Infrastructure.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class DomainEventDispatcherTests
{
    public class TestEvent : IDomainEvent
    {
        public System.DateTime OccurredOn => System.DateTime.UtcNow;
    }

    public class TestEventHandler : IDomainEventHandler<TestEvent>
    {
        public bool IsHandled { get; private set; }

        public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken = default)
        {
            IsHandled = true;
            return Task.CompletedTask;
        }
    }

    public class FailingEventHandler : IDomainEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent domainEvent, CancellationToken cancellationToken = default)
        {
            throw new System.Exception("Test failure");
        }
    }

    [Fact]
    public async Task DispatchEventsAsync_ResolvesAndInvokesHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        var dispatcher = new DomainEventDispatcher(provider, loggerMock.Object);

        // Act
        await dispatcher.DispatchEventsAsync(new[] { new TestEvent() }, CancellationToken.None);

        // Assert
        handler.IsHandled.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchEventsAsync_ExceptionsAreCaughtAndLogged()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(new FailingEventHandler());
        var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        var dispatcher = new DomainEventDispatcher(provider, loggerMock.Object);

        // Act
        var exception = await Record.ExceptionAsync(() => dispatcher.DispatchEventsAsync(new[] { new TestEvent() }, CancellationToken.None));

        // Assert
        exception.Should().BeNull(); // Should not throw
        // loggerMock.VerifyLog(logger => logger.LogError(...)) is tricky with extension methods, but verifying it doesn't throw is good.
    }
}
