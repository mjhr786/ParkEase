using ParkingApp.Notifications.Contracts;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Messaging.Application.Commands.Chat;
using ParkingApp.Application.Interfaces;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class ChatCommandsTests
{
    private readonly Mock<IMessagingUnitOfWork> _mockMessaging;
    private readonly Mock<IConversationRepository> _mockConversationRepo;
    private readonly Mock<IChatMessageRepository> _mockChatMessageRepo;
    private readonly Mock<IParkingSpaceLookup> _mockParkingLookup;
    private readonly Mock<IUserLookup> _mockUserLookup;
    private readonly Mock<ILogger<SendMessageHandler>> _mockSendLogger;
    private readonly Mock<IDeferredPushNotificationService> _mockDeferredPush;
    private readonly Mock<ICacheService> _mockCache;

    public ChatCommandsTests()
    {
        _mockMessaging = new Mock<IMessagingUnitOfWork>();
        _mockConversationRepo = new Mock<IConversationRepository>();
        _mockChatMessageRepo = new Mock<IChatMessageRepository>();
        _mockParkingLookup = new Mock<IParkingSpaceLookup>();
        _mockUserLookup = new Mock<IUserLookup>();

        _mockMessaging.Setup(u => u.Conversations).Returns(_mockConversationRepo.Object);
        _mockMessaging.Setup(u => u.ChatMessages).Returns(_mockChatMessageRepo.Object);

        _mockSendLogger = new Mock<ILogger<SendMessageHandler>>();
        _mockDeferredPush = new Mock<IDeferredPushNotificationService>();
        _mockCache = new Mock<ICacheService>();
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private SendMessageHandler CreateSendHandler() =>
        new(_mockMessaging.Object, _mockParkingLookup.Object, _mockUserLookup.Object, _mockSendLogger.Object, _mockDeferredPush.Object, _mockCache.Object);

    // SendMessageHandler Tests
    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenContentEmpty()
    {
        var handler = CreateSendHandler();

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), "   ", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenContentTooLong()
    {
        var handler = CreateSendHandler();

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), new string('A', 2001), null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("cannot exceed 2000 characters");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenParkingNotFound()
    {
        var handler = CreateSendHandler();
        _mockParkingLookup.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpaceSummary?)null);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), "Test", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Parking space not found");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenUnauthorizedForConversation()
    {
        var handler = CreateSendHandler();
        var parkingId = Guid.NewGuid();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), VendorId = Guid.NewGuid(), ParkingSpaceId = parkingId };

        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(parkingId, "Hello", conversation.Id)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized for this conversation");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldSucceed_WithExistingConversation()
    {
        var handler = CreateSendHandler();
        var senderId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = senderId,
            VendorId = vendorId,
            ParkingSpaceId = parkingId
        };
        var user = new UserSummary(senderId, "a@b.com", "Ada", "Lovelace");

        // Existing conversation path must not hit parking lookup
        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        _mockUserLookup.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new SendMessageCommand(senderId, new SendMessageDto(parkingId, "Hello", conversation.Id)));

        res.Success.Should().BeTrue();
        res.Data!.SenderName.Should().Be("Ada Lovelace");
        res.Data.RecipientId.Should().Be(vendorId);
        _mockChatMessageRepo.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMessaging.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUserLookup.Verify(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()), Times.Once);
        _mockParkingLookup.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDeferredPush.Verify(
            p => p.ScheduleSendToUser(vendorId, It.IsAny<PushNotificationPayload>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenSendingToSelf()
    {
        var handler = CreateSendHandler();
        var senderId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpaceSummary(parkingId, senderId, "My Lot", true, 10, "IndividualVendor");

        _mockParkingLookup.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByParticipantsAsync(parkingId, senderId, It.IsAny<CancellationToken>())).ReturnsAsync((Conversation?)null);
        _mockConversationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Conversation>());

        var res = await handler.HandleAsync(new SendMessageCommand(senderId, new SendMessageDto(parkingId, "Hello", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Cannot start a conversation with yourself");
    }

    // MarkMessagesReadHandler Tests
    [Fact]
    public async Task MarkMessagesReadHandler_ShouldFail_WhenConversationNotFound()
    {
        var handler = new MarkMessagesReadHandler(_mockMessaging.Object, _mockCache.Object);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conversation?)null);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Conversation not found");
    }

    [Fact]
    public async Task MarkMessagesReadHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new MarkMessagesReadHandler(_mockMessaging.Object, _mockCache.Object);
        var conversation = new Conversation { UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task MarkMessagesReadHandler_ShouldSucceed()
    {
        var handler = new MarkMessagesReadHandler(_mockMessaging.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = userId, VendorId = vendorId };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(userId, conversation.Id));

        res.Success.Should().BeTrue();
        res.Data!.Marked.Should().BeTrue();
        res.Data.OtherParticipantId.Should().Be(vendorId);
        _mockChatMessageRepo.Verify(r => r.MarkAsReadAsync(conversation.Id, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMessaging.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
