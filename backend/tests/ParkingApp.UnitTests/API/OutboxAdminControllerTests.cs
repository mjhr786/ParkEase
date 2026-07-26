using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Shared.Outbox;
using ParkingApp.Application.DTOs;

namespace ParkingApp.UnitTests.API;

public class OutboxAdminControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly OutboxAdminController _controller;

    public OutboxAdminControllerTests()
    {
        _controller = new OutboxAdminController(_dispatcher.Object);
    }

    [Fact]
    public async Task List_ReturnsOk()
    {
        var response = new ApiResponse<OutboxMessageListResultDto>(true, null, null);
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetOutboxMessagesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.List(cancellationToken: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetOutboxMessageByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<OutboxMessageDto>(false, "not found", null));

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Requeue_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RequeueOutboxMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.Requeue(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Requeue_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RequeueOutboxMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(false, "cannot requeue", false));

        var result = await _controller.Requeue(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RequeueAllFailed_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RequeueAllFailedOutboxMessagesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<int>(true, null, 3));

        var result = await _controller.RequeueAllFailed(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ProcessNow_ReturnsOk()
    {
        var response = new ApiResponse<ProcessOutboxResultDto>(
            true, null, new ProcessOutboxResultDto(5, "done"));
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessOutboxNowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.ProcessNow(25, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<ProcessOutboxNowCommand>(c => c.BatchSize == 25),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
