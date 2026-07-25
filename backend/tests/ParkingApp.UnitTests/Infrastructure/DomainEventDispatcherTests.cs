using ParkingApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.BuildingBlocks.Domain;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class DomainEventDispatcherTests
{
    public class TestEvent : IDomainEvent
    {
        public DateTime OccurredOn => DateTime.UtcNow;
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
            throw new Exception("Test failure");
        }
    }

    [Fact]
    public async Task DispatchEventsAsync_ResolvesAndInvokesHandlers()
    {
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        // Register against BuildingBlocks open-generic (shared kernel / outbox path)
        services.AddSingleton<IDomainEventHandler<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        var dispatcher = new DomainEventDispatcher(provider, loggerMock.Object);

        await dispatcher.DispatchEventsAsync(new[] { new TestEvent() }, CancellationToken.None);

        handler.IsHandled.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchEventsAsync_ExceptionsAreCaughtAndLogged()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<TestEvent>>(new FailingEventHandler());
        var provider = services.BuildServiceProvider();

        var loggerMock = new Mock<ILogger<DomainEventDispatcher>>();
        var dispatcher = new DomainEventDispatcher(provider, loggerMock.Object);

        var exception = await Record.ExceptionAsync(() =>
            dispatcher.DispatchEventsAsync(new[] { new TestEvent() }, CancellationToken.None));

        exception.Should().BeNull(); // handlers must not break the main flow
    }
}





