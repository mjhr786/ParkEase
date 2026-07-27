using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Messaging.Application.Commands.Chat;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Notifications.Contracts;

namespace ParkingApp.UnitTests.Messaging;

public class SendMessageHandlerTests
{
    private readonly Mock<IMessagingUnitOfWork> _uow = new();
    private readonly Mock<IConversationRepository> _conversations = new();
    private readonly Mock<IChatMessageRepository> _messages = new();
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IDeferredPushNotificationService> _push = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Guid _senderId = Guid.NewGuid();
    private readonly Guid _vendorId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public SendMessageHandlerTests()
    {
        _uow.Setup(x => x.Conversations).Returns(_conversations.Object);
        _uow.Setup(x => x.ChatMessages).Returns(_messages.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _conversations.Setup(x => x.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation c, CancellationToken _) => c);
        _messages.Setup(x => x.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatMessage m, CancellationToken _) => m);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _users.Setup(x => x.GetByIdAsync(_senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_senderId, "s@x.com", "Send", "Er"));
    }

    private SendMessageHandler CreateSut() =>
        new(_uow.Object, _parking.Object, _users.Object, NullLogger<SendMessageHandler>.Instance,
            _push.Object, _cache.Object);

    [Fact]
    public async Task Send_WhenEmptyContent_ReturnsFailure()
    {
        var result = await CreateSut().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "   ")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("empty");
    }

    [Fact]
    public async Task Send_WhenTooLong_ReturnsFailure()
    {
        var result = await CreateSut().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, new string('x', 2001))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("2000");
    }

    [Fact]
    public async Task Send_WhenConversationUnauthorized_ReturnsFailure()
    {
        var conversation = new Conversation
        {
            ParkingSpaceId = _spaceId,
            UserId = Guid.NewGuid(),
            VendorId = Guid.NewGuid()
        };
        _conversations.Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var result = await CreateSut().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "hi", conversation.Id)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Send_WhenSelfChatNew_ReturnsFailure()
    {
        _parking.Setup(x => x.GetByIdAsync(_spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(_spaceId, _senderId, "Lot", true, 5, "IndividualVendor"));
        _conversations.Setup(x => x.GetByParticipantsAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);
        _conversations.Setup(x => x.GetSoleByVendorAndSpaceAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await CreateSut().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "hello")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("yourself");
    }

    [Fact]
    public async Task Send_WhenNewConversation_CreatesAndSends()
    {
        _parking.Setup(x => x.GetByIdAsync(_spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(_spaceId, _vendorId, "Lot", true, 5, "IndividualVendor"));
        _conversations.Setup(x => x.GetByParticipantsAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);
        _conversations.Setup(x => x.GetSoleByVendorAndSpaceAsync(_spaceId, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await CreateSut().HandleAsync(new SendMessageCommand(
            _senderId, new SendMessageDto(_spaceId, "Is this available?")));

        result.Success.Should().BeTrue();
        result.Data!.Content.Should().Be("Is this available?");
        result.Data.RecipientId.Should().Be(_vendorId);
        _messages.Verify(x => x.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _push.Verify(x => x.ScheduleSendToUser(_vendorId, It.IsAny<PushNotificationPayload>()), Times.Once);
        _conversations.Verify(x => x.AddAsync(It.Is<Conversation>(c => c.LastMessageAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkRead_WhenUnauthorized_ReturnsFailure()
    {
        var conversation = new Conversation
        {
            ParkingSpaceId = _spaceId,
            UserId = Guid.NewGuid(),
            VendorId = Guid.NewGuid()
        };
        _conversations.Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var handler = new MarkMessagesReadHandler(_uow.Object, _cache.Object);
        var result = await handler.HandleAsync(new MarkMessagesReadCommand(_senderId, conversation.Id));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MarkRead_WhenParticipant_Succeeds()
    {
        var conversation = new Conversation
        {
            ParkingSpaceId = _spaceId,
            UserId = _senderId,
            VendorId = _vendorId
        };
        _conversations.Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _messages.Setup(x => x.MarkAsReadAsync(conversation.Id, _senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new MarkMessagesReadHandler(_uow.Object, _cache.Object);
        var result = await handler.HandleAsync(new MarkMessagesReadCommand(_senderId, conversation.Id));

        result.Success.Should().BeTrue();
        result.Data!.OtherParticipantId.Should().Be(_vendorId);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
