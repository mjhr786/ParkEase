using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Behaviors;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Validators;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Persistence;
using Xunit;

namespace ParkingApp.UnitTests.CQRS;

public class DispatcherPipelineTests
{
    private sealed class CapturingHandler : ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>
    {
        public int CallCount { get; private set; }

        public Task<ApiResponse<BookingDto>> HandleAsync(
            CreateBookingCommand command,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ApiResponse<BookingDto>(true, "ok", null));
        }
    }

    [Fact]
    public async Task SendAsync_WhenCommandInvalid_ReturnsValidationFailure_WithoutCallingHandler()
    {
        var handler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>>(_ => handler);
        services.AddScoped<IValidator<CreateBookingCommand>, CreateBookingCommandValidator>();
        services.AddScoped<IDispatcherBehavior, LoggingBehavior>();
        services.AddScoped<IDispatcherBehavior, ValidationBehavior>();
        services.AddScoped(_ => new Mock<IUnitOfWorkTransaction>().Object);
        services.AddScoped<IDispatcher, Dispatcher>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Start in the past — fails validator
        var command = new CreateBookingCommand(
            Guid.NewGuid(),
            Guid.Empty, // also invalid
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1),
            PricingType.Hourly,
            VehicleType.Car,
            null, null, null, null, null);

        var result = await dispatcher.SendAsync(command);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Validation failed");
        result.Errors.Should().NotBeNullOrEmpty();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenCommandValid_InvokesHandler()
    {
        var handler = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>>(_ => handler);
        services.AddScoped<IValidator<CreateBookingCommand>, CreateBookingCommandValidator>();
        services.AddScoped<IDispatcherBehavior, LoggingBehavior>();
        services.AddScoped<IDispatcherBehavior, ValidationBehavior>();
        services.AddScoped(_ => new Mock<IUnitOfWorkTransaction>().Object);
        services.AddScoped<IDispatcher, Dispatcher>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var start = DateTime.UtcNow.AddDays(1);
        var command = new CreateBookingCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start,
            start.AddHours(2),
            PricingType.Hourly,
            VehicleType.Car,
            null, null, null, null, null);

        var result = await dispatcher.SendAsync(command);

        result.Success.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task TransactionBehavior_WhenNotTransactional_DoesNotBeginTransaction()
    {
        var uow = new Mock<IUnitOfWorkTransaction>();
        var behavior = new TransactionBehavior(uow.Object);
        var called = false;

        var result = await behavior.HandleAsync(
            new object(),
            isCommand: true,
            next: () =>
            {
                called = true;
                return Task.FromResult(42);
            },
            CancellationToken.None);

        result.Should().Be(42);
        called.Should().BeTrue();
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class TxCommand : ICommand<int>, ITransactionalCommand
    {
    }

    [Fact]
    public async Task TransactionBehavior_WhenTransactional_BeginsAndCommits()
    {
        var uow = new Mock<IUnitOfWorkTransaction>();
        var behavior = new TransactionBehavior(uow.Object);

        var result = await behavior.HandleAsync(
            new TxCommand(),
            isCommand: true,
            next: () => Task.FromResult(7),
            CancellationToken.None);

        result.Should().Be(7);
        uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoggingBehavior_PropagatesResult()
    {
        var behavior = new LoggingBehavior(NullLogger<LoggingBehavior>.Instance);
        var result = await behavior.HandleAsync(
            new object(),
            isCommand: false,
            next: () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task LoggingBehavior_DoesNotThrow_WhenSlowThresholdConfigured()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new PerformanceLoggingOptions { SlowRequestMs = 0 });
        var behavior = new LoggingBehavior(NullLogger<LoggingBehavior>.Instance, options);

        var result = await behavior.HandleAsync(
            new object(),
            isCommand: true,
            next: async () =>
            {
                await Task.Delay(1);
                return 42;
            },
            CancellationToken.None);

        result.Should().Be(42);
    }
}





