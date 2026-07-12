using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Shared.Outbox;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS;

public class OutboxAdminHandlerTests
{
    [Fact]
    public async Task GetOutboxMessagesHandler_DelegatesToStore()
    {
        var store = new Mock<IOutboxAdminStore>();
        var expected = new OutboxMessageListResultDto(
            new List<OutboxMessageDto>(),
            0, 1, 50, 0,
            new OutboxSummaryDto(0, 0, 0, 1, 1));
        store.Setup(s => s.ListAsync(OutboxMessageStatusDto.Failed, null, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetOutboxMessagesHandler(store.Object);
        var result = await handler.HandleAsync(new GetOutboxMessagesQuery(OutboxMessageStatusDto.Failed));

        result.Success.Should().BeTrue();
        result.Data!.Summary.Failed.Should().Be(1);
    }

    [Fact]
    public async Task RequeueOutboxMessageHandler_WhenNotFound_ReturnsFailure()
    {
        var store = new Mock<IOutboxAdminStore>();
        store.Setup(s => s.RequeueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new RequeueOutboxMessageHandler(store.Object);
        var result = await handler.HandleAsync(new RequeueOutboxMessageCommand(Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessOutboxNowHandler_ReturnsProcessedCount()
    {
        var processor = new Mock<IOutboxProcessor>();
        processor.Setup(p => p.ProcessPendingAsync(25, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var handler = new ProcessOutboxNowHandler(processor.Object);
        var result = await handler.HandleAsync(new ProcessOutboxNowCommand(25));

        result.Success.Should().BeTrue();
        result.Data!.ProcessedCount.Should().Be(3);
    }
}
