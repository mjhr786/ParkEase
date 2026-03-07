using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ParkingApp.Application.CQRS;
using Xunit;

namespace ParkingApp.UnitTests.CQRS;

public class DispatcherTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public DispatcherTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
    }

    // Dummy Command and Handler
    public class DummyCommand : ICommand<int> { }
    public class DummyCommandHandler : ICommandHandler<DummyCommand, int>
    {
        public Task<int> HandleAsync(DummyCommand command, CancellationToken cancellationToken = default) => Task.FromResult(42);
    }

    // Dummy Query and Handler
    public class DummyQuery : IQuery<string> { }
    public class DummyQueryHandler : IQueryHandler<DummyQuery, string>
    {
        public Task<string> HandleAsync(DummyQuery query, CancellationToken cancellationToken = default) => Task.FromResult("Result");
    }

    [Fact]
    public async Task SendAsync_ShouldThrow_WhenHandlerNotRegistered()
    {
        _mockServiceProvider.Setup(sp => sp.GetService(It.IsAny<Type>())).Returns(null);
        var dispatcher = new Dispatcher(_mockServiceProvider.Object);

        Func<Task> act = async () => await dispatcher.SendAsync(new DummyCommand());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No handler registered*");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnResult_WhenHandlerRegistered()
    {
        var handler = new DummyCommandHandler();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ICommandHandler<DummyCommand, int>))).Returns(handler);
        var dispatcher = new Dispatcher(_mockServiceProvider.Object);

        var result = await dispatcher.SendAsync(new DummyCommand());

        result.Should().Be(42);
    }

    [Fact]
    public async Task QueryAsync_ShouldThrow_WhenHandlerNotRegistered()
    {
        _mockServiceProvider.Setup(sp => sp.GetService(It.IsAny<Type>())).Returns(null);
        var dispatcher = new Dispatcher(_mockServiceProvider.Object);

        Func<Task> act = async () => await dispatcher.QueryAsync(new DummyQuery());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No handler registered*");
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnResult_WhenHandlerRegistered()
    {
        var handler = new DummyQueryHandler();
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IQueryHandler<DummyQuery, string>))).Returns(handler);
        var dispatcher = new Dispatcher(_mockServiceProvider.Object);

        var result = await dispatcher.QueryAsync(new DummyQuery());

        result.Should().Be("Result");
    }
}
