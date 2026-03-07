using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.CQRS.Queries.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Common;
using ParkingApp.Notifications.Hubs;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class ChatControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IHubContext<ChatHub>> _chatHubContextMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _chatHubContextMock = new Mock<IHubContext<ChatHub>>();
        _clientProxyMock = new Mock<IClientProxy>();
        _hubClientsMock = new Mock<IHubClients>();

        _chatHubContextMock.Setup(x => x.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(x => x.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        _controller = new ChatController(_dispatcherMock.Object, _chatHubContextMock.Object);
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetConversations_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ConversationListDto>(true, "Success", new ConversationListDto(new List<ConversationDto>(), 0, 1, 20, 0), null)));

        var result = await _controller.GetConversations(1, 20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMessages_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock.Setup(d => d.QueryAsync(It.Is<GetMessagesQuery>(q => q.ConversationId == convId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<List<ChatMessageDto>>(true, "Success", new List<ChatMessageDto>(), null)));

        var result = await _controller.GetMessages(convId, 1, 50, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SendMessage_ReturnsOk_PushesToSignalR()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new SendMessageDto(Guid.NewGuid(), "Hello");
        var messageDto = new ChatMessageDto(Guid.NewGuid(), convId, Guid.NewGuid(), "Sender", "Hello", false, DateTime.UtcNow);

        _dispatcherMock.Setup(d => d.SendAsync(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ChatMessageDto>(true, "Success", messageDto, null)));

        var clist = new ConversationListDto(
            new List<ConversationDto> 
            { 
                new ConversationDto(convId, Guid.NewGuid(), "Test", Guid.NewGuid(), "Participant", null, null, 0, DateTime.UtcNow) 
            }, 1, 1, 10, 1);
        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ConversationListDto>(true, "Success", clist, null)));

        var result = await _controller.SendMessage(dto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiRes = okResult.Value.Should().BeOfType<ApiResponse<ChatMessageDto>>().Subject;
        apiRes.Success.Should().BeTrue();

        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveMessage", new object[] { messageDto }, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk_PushesToSignalR()
    {
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock.Setup(d => d.SendAsync(It.Is<MarkMessagesReadCommand>(c => c.ConversationId == convId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        var clist = new ConversationListDto(
            new List<ConversationDto> 
            { 
                new ConversationDto(convId, Guid.NewGuid(), "Test", Guid.NewGuid(), "Participant", null, null, 0, DateTime.UtcNow) 
            }, 1, 1, 10, 1);
        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ConversationListDto>(true, "Success", clist, null)));

        var result = await _controller.MarkAsRead(convId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _clientProxyMock.Verify(c => c.SendCoreAsync("MessagesRead", new object[] { convId }, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        var clist = new ConversationListDto(
            new List<ConversationDto> 
            { 
                new ConversationDto(Guid.NewGuid(), Guid.NewGuid(), "Test", Guid.NewGuid(), "Participant", null, null, 3, DateTime.UtcNow) 
            }, 1, 1, 10, 1);
        _dispatcherMock.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ConversationListDto>(true, "Success", clist, null)));

        var result = await _controller.GetUnreadCount(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
    }
}
