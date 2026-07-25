using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Infrastructure.Outbox;

namespace ParkingApp.UnitTests.Infrastructure;

public class OutboxBackgroundServiceTests
{
    private static OutboxBackgroundService CreateService()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var monitor = new TestOptionsMonitor(new OutboxOptions());
        return new OutboxBackgroundService(
            scopeFactory,
            monitor,
            NullLogger<OutboxBackgroundService>.Instance);
    }

    [Fact]
    public void ResolveDelaySeconds_AfterWork_UsesBusyInterval()
    {
        var svc = CreateService();
        var opts = new OutboxOptions
        {
            PollIntervalSeconds = 15,
            BusyPollIntervalSeconds = 5,
            EmptyBackoffMaxSeconds = 60
        };

        svc.ResolveDelaySeconds(opts, processedCount: 3).Should().Be(5);
        // Busy reset: next empty starts at base again
        svc.ResolveDelaySeconds(opts, processedCount: 0).Should().Be(15);
    }

    [Fact]
    public void ResolveDelaySeconds_EmptyQueue_BacksOffToMax()
    {
        var svc = CreateService();
        var opts = new OutboxOptions
        {
            PollIntervalSeconds = 15,
            BusyPollIntervalSeconds = 5,
            EmptyBackoffMaxSeconds = 60
        };

        svc.ResolveDelaySeconds(opts, 0).Should().Be(15);
        svc.ResolveDelaySeconds(opts, 0).Should().Be(30);
        svc.ResolveDelaySeconds(opts, 0).Should().Be(60);
        svc.ResolveDelaySeconds(opts, 0).Should().Be(60); // capped
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<OutboxOptions>
    {
        public TestOptionsMonitor(OutboxOptions current) => CurrentValue = current;
        public OutboxOptions CurrentValue { get; }
        public OutboxOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<OutboxOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
