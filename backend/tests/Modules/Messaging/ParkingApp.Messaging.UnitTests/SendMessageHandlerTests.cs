using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Messaging.Application.Commands.Chat;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Notifications.Contracts;

namespace ParkingApp.Messaging.UnitTests;

public class SendMessageHandlerTests
{
    private readonly Mock<IMessagingUnitOfWork> _messaging = new();
    private readonly Mock<IConversationRepository> _conversations = new();
    private readonly Mock<IChatMessageRepository> _messages = new();
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IDeferredPushNotificationService> _push = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ILogger<SendMessageHandler>> _logger = new();
    private readonly Guid _senderId = Guid.NewGuid();
    private readonly Guid _vendorId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public SendMessageHandlerTests()
    {
        _messaging.Setup(x => x.Conversations).Returns(_conversations.Object);
        _messaging.Setup(x => x.ChatMessages).Returns(_messages.Object);
        _messaging.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(x => x.GetByIdAsync(_senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_senderId, "u@x.com", "Ada", "Lovelace"));
    }

    private SendMessageHandler CreateHandler() =>
        new(_messaging.Object, _parking.Object, _users.Object, _logger.Object, _push.Object, _cache.Object);

    [Fact]
    public async Task Send_EmptyContent_Fails()
    {
        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "   ")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task Send_TooLong_Fails()
    {
        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, new string('a', 2001))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("2000");
    }

    [Fact]
    public async Task Send_WhenUnauthorizedOnConversation_Fails()
    {
        var conversationId = Guid.NewGuid();
        _conversations.Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation
            {
                Id = conversationId,
                ParkingSpaceId = _spaceId,
                UserId = Guid.NewGuid(),
                VendorId = Guid.NewGuid()
            });

        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "hi", conversationId)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Send_WhenParkingMissing_Fails()
    {
        _parking.Setup(x => x.GetByIdAsync(_spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceSummary?)null);
        _conversations.Setup(x => x.GetByParticipantsAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "hello")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Parking space not found");
    }

    [Fact]
    public async Task Send_WhenSelfChat_Fails()
    {
        _parking.Setup(x => x.GetByIdAsync(_spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(_spaceId, _senderId, "Lot", true, 5, "IndividualVendor"));
        _conversations.Setup(x => x.GetByParticipantsAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);
        _conversations.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Conversation>());

        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "hello")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("yourself");
    }

    [Fact]
    public async Task Send_OnExistingConversation_Succeeds()
    {
        var conversationId = Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = conversationId,
            ParkingSpaceId = _spaceId,
            UserId = _senderId,
            VendorId = _vendorId
        };
        _conversations.Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _messages.Setup(x => x.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatMessage m, CancellationToken _) => m);

        var result = await CreateHandler().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "Hello vendor", conversationId)));

        result.Success.Should().BeTrue();
        result.Data!.Content.Should().Be("Hello vendor");
        result.Data.SenderName.Should().Be("Ada Lovelace");
        result.Data.RecipientId.Should().Be(_vendorId);
        conversation.LastMessagePreview.Should().Be("Hello vendor");
        _push.Verify(x => x.ScheduleSendToUser(_vendorId, It.IsAny<PushNotificationPayload>()), Times.Once);
        _messaging.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkRead_WhenNotFound_Fails()
    {
        _conversations.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var handler = new MarkMessagesReadHandler(_messaging.Object, _cache.Object);
        var result = await handler.HandleAsync(new MarkMessagesReadCommand(_senderId, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task MarkRead_WhenParticipant_Succeeds()
    {
        var conversationId = Guid.NewGuid();
        _conversations.Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Conversation
            {
                Id = conversationId,
                ParkingSpaceId = _spaceId,
                UserId = _senderId,
                VendorId = _vendorId
            });
        _messages.Setup(x => x.MarkAsReadAsync(conversationId, _senderId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new MarkMessagesReadHandler(_messaging.Object, _cache.Object);
        var result = await handler.HandleAsync(new MarkMessagesReadCommand(_senderId, conversationId));

        result.Success.Should().BeTrue();
        result.Data!.OtherParticipantId.Should().Be(_vendorId);
        _cache.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
