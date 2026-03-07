using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.BuildingBlocks.Logging;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class OperationTimerTests
{
    private readonly Mock<ILogger> _loggerMock;

    public OperationTimerTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    [Fact]
    public async Task OperationTimer_ShouldLogStartAndComplete()
    {
        using (var timer = new OperationTimer(_loggerMock.Object, "TestOp"))
        {
            await Task.Delay(10);
            timer.Elapsed.TotalMilliseconds.Should().BeGreaterThan(0);
        }

        VerifyLog(LogLevel.Information, Times.Exactly(2));
    }

    [Fact]
    public void TimerFactory_Time_ShouldReturnTimer()
    {
        using var timer = _loggerMock.Object.Time("TestOp");
        timer.Should().NotBeNull();
        VerifyLog(LogLevel.Information, Times.Once()); // Starts
    }

    [Fact]
    public async Task TimerFactory_TimeAsyncWithResult_ShouldReturnResult()
    {
        var result = await _loggerMock.Object.TimeAsync("TestOp", async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        result.Should().Be(42);
        VerifyLog(LogLevel.Information, Times.Exactly(2));
    }

    [Fact]
    public async Task TimerFactory_TimeAsync_ShouldExecute()
    {
        bool executed = false;
        await _loggerMock.Object.TimeAsync("TestOp", async () =>
        {
            await Task.Delay(10);
            executed = true;
        });

        executed.Should().BeTrue();
        VerifyLog(LogLevel.Information, Times.Exactly(2));
    }

    private void VerifyLog(LogLevel level, Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            times);
    }
}
