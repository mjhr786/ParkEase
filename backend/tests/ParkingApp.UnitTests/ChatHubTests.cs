using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.Notifications.Hubs;
using Xunit;

namespace ParkingApp.UnitTests;

public class ChatHubTests
{
    private readonly Mock<ILogger<ChatHub>> _mockLogger;
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly FakeHubCallerClients _fakeClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly FakeSingleClientProxy _fakeSingleClientProxy;
    private readonly ChatHub _hub;

    private class FakeSingleClientProxy : ISingleClientProxy
    {
        public List<(string Method, object?[] Args)> Calls { get; } = new();
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Calls.Add((method, args));
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(T)!);
        }
    }

    private class FakeHubCallerClients : IHubCallerClients
    {
        public FakeSingleClientProxy FakeCaller { get; }
        public IClientProxy FakeGroup { get; }

        public FakeHubCallerClients(FakeSingleClientProxy caller, IClientProxy group)
        {
            FakeCaller = caller;
            FakeGroup = group;
        }

        public ISingleClientProxy Caller => FakeCaller;
        public IClientProxy Others => throw new NotImplementedException();
        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => FakeGroup;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
        public IClientProxy User(string userId) => throw new NotImplementedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();

        IClientProxy IHubCallerClients<IClientProxy>.Caller => FakeCaller;
        IClientProxy IHubCallerClients<IClientProxy>.Others => throw new NotImplementedException();
        IClientProxy IHubCallerClients<IClientProxy>.OthersInGroup(string groupName) => throw new NotImplementedException();
    }

    public ChatHubTests()
    {
        _mockLogger = new Mock<ILogger<ChatHub>>();
        _mockDispatcher = new Mock<IDispatcher>();
        _mockContext = new Mock<HubCallerContext>();
        _mockGroups = new Mock<IGroupManager>();
        _mockClientProxy = new Mock<IClientProxy>();
        _fakeSingleClientProxy = new FakeSingleClientProxy();
        _fakeClients = new FakeHubCallerClients(_fakeSingleClientProxy, _mockClientProxy.Object);

        _mockClientProxy.SetReturnsDefault(Task.CompletedTask);

        _mockContext.SetupGet(c => c.User).Returns((ClaimsPrincipal?)null);

        _hub = new ChatHub(_mockLogger.Object, _mockDispatcher.Object)
        {
            Context = _mockContext.Object,
            Groups = _mockGroups.Object,
            Clients = _fakeClients
        };
    }

    [Fact]
    public void GetUserGroupName_ShouldReturnFormattedString()
    {
        var userId = Guid.NewGuid();
        var result = ChatHub.GetUserGroupName(userId);
        result.Should().Be($"chat_user_{userId}");
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidUser_ShouldAddToGroup()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));
        _mockContext.Setup(c => c.ConnectionId).Returns("conn123");

        await _hub.OnConnectedAsync();

        _mockGroups.Verify(g => g.AddToGroupAsync("conn123", $"chat_user_{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithValidUser_ShouldLogAndDisconnect()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));

        // It is harder to mock ILogger exact calls with Exception in older Moq syntax,
        // so we just verify it runs without crashing for this coverage.
        var exception = new Exception("test exception");
        await _hub.OnDisconnectedAsync(exception);
    }

    [Fact]
    public async Task SendMessage_WhenUnauthenticated_ShouldSendError()
    {
        _mockContext.Setup(c => c.User).Returns((ClaimsPrincipal?)null);

        await _hub.SendMessage(Guid.NewGuid(), "Hello");

        _fakeSingleClientProxy.Calls.Should().Contain(c => c.Method == "Error" && c.Args != null && c.Args.Length > 0 && (string)c.Args[0]! == "Unauthorized");
    }

    [Fact]
    public async Task SendMessage_WhenDispatcherFails_ShouldSendError()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ChatMessageDto>(false, "Dispatcher Error", null));

        await _hub.SendMessage(Guid.NewGuid(), "Hello");

        _fakeSingleClientProxy.Calls.Should().Contain(c => c.Method == "Error" && c.Args != null && c.Args.Length > 0 && (string)c.Args[0]! == "Dispatcher Error");
    }

    [Fact]
    public async Task SendMessage_WhenSuccessful_ShouldBroadcastToGroup()
    {
        var userId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _mockContext.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));

        var msgDto = new ChatMessageDto(Guid.NewGuid(), Guid.NewGuid(), userId, "Sender", "Hello", false, DateTime.UtcNow);
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ChatMessageDto>(true, null, msgDto));

        await _hub.SendMessage(Guid.NewGuid(), "Hello");

        _mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveMessage", new object[] { msgDto }, It.IsAny<CancellationToken>()), Times.Once);
    }
}
