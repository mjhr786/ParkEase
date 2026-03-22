using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.BuildingBlocks.Logging;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class LoggingExtensionsTests
{
    private readonly Mock<ILogger> _loggerMock;

    public LoggingExtensionsTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    [Fact]
    public void LogOperationStart_ShouldLog()
    {
        _loggerMock.Object.LogOperationStart("TestOp", "arg1");
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogOperationComplete_ShouldLog()
    {
        _loggerMock.Object.LogOperationComplete("TestOp", TimeSpan.FromSeconds(1));
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogOperationFailed_ShouldLog()
    {
        _loggerMock.Object.LogOperationFailed("TestOp", new Exception());
        VerifyLog(LogLevel.Error);
    }

    [Fact]
    public void LogEntityCreated_ShouldLog()
    {
        _loggerMock.Object.LogEntityCreated<object>(1);
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogEntityUpdated_ShouldLog()
    {
        _loggerMock.Object.LogEntityUpdated<object>(1);
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogEntityDeleted_ShouldLog()
    {
        _loggerMock.Object.LogEntityDeleted<object>(1);
        VerifyLog(LogLevel.Warning);
    }

    [Fact]
    public void LogEntityNotFound_ShouldLog()
    {
        _loggerMock.Object.LogEntityNotFound<object>(1);
        VerifyLog(LogLevel.Warning);
    }

    [Fact]
    public void LogCacheHit_ShouldLog()
    {
        _loggerMock.Object.LogCacheHit("key");
        VerifyLog(LogLevel.Debug);
    }

    [Fact]
    public void LogCacheMiss_ShouldLog()
    {
        _loggerMock.Object.LogCacheMiss("key");
        VerifyLog(LogLevel.Debug);
    }

    [Fact]
    public void LogCacheInvalidated_ShouldLog()
    {
        _loggerMock.Object.LogCacheInvalidated("key");
        VerifyLog(LogLevel.Debug);
    }

    [Fact]
    public void LogUserAuthenticated_ShouldLog()
    {
        _loggerMock.Object.LogUserAuthenticated(1, "test@test.com");
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogAuthenticationFailed_ShouldLog()
    {
        _loggerMock.Object.LogAuthenticationFailed("test@test.com", "reason");
        VerifyLog(LogLevel.Warning);
    }

    [Fact]
    public void LogUnauthorizedAccess_ShouldLog()
    {
        _loggerMock.Object.LogUnauthorizedAccess(1, "res");
        VerifyLog(LogLevel.Warning);
    }

    [Fact]
    public void LogPaymentInitiated_ShouldLog()
    {
        _loggerMock.Object.LogPaymentInitiated(1, 100m);
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogPaymentCompleted_ShouldLog()
    {
        _loggerMock.Object.LogPaymentCompleted(1, "tx");
        VerifyLog(LogLevel.Information);
    }

    [Fact]
    public void LogPaymentFailed_ShouldLog()
    {
        _loggerMock.Object.LogPaymentFailed(1, "err");
        VerifyLog(LogLevel.Error);
    }

    [Fact]
    public void LogExternalServiceCall_ShouldLog()
    {
        _loggerMock.Object.LogExternalServiceCall("Svc", "Op");
        VerifyLog(LogLevel.Debug);
    }

    [Fact]
    public void LogExternalServiceError_ShouldLog()
    {
        _loggerMock.Object.LogExternalServiceError("Svc", new Exception());
        VerifyLog(LogLevel.Error);
    }

    private void VerifyLog(LogLevel level)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
