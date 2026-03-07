using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class ChatCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IConversationRepository> _mockConversationRepo;
    private readonly Mock<IChatMessageRepository> _mockChatMessageRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ILogger<SendMessageHandler>> _mockSendLogger;

    public ChatCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockConversationRepo = new Mock<IConversationRepository>();
        _mockChatMessageRepo = new Mock<IChatMessageRepository>();
        _mockUserRepo = new Mock<IUserRepository>();

        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Conversations).Returns(_mockConversationRepo.Object);
        _mockUow.Setup(u => u.ChatMessages).Returns(_mockChatMessageRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);

        _mockSendLogger = new Mock<ILogger<SendMessageHandler>>();
    }

    // SendMessageHandler Tests
    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenContentEmpty()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), "   ", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenContentTooLong()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), new string('A', 2001), null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("cannot exceed 2000 characters");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenParkingNotFound()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(Guid.NewGuid(), "Test", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Parking space not found");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenUnauthorizedForConversation()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);
        var parking = new ParkingSpace { Id = Guid.NewGuid() };
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), new SendMessageDto(parking.Id, "Hello", conversation.Id)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized for this conversation");
    }

    [Fact]
    public async Task SendMessageHandler_ShouldSucceed_WithExistingConversation()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);
        var senderId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid() };
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = senderId, VendorId = Guid.NewGuid() };
        var user = new User { Id = senderId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
        _mockUserRepo.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new SendMessageCommand(senderId, new SendMessageDto(parking.Id, "Hello", conversation.Id)));

        res.Success.Should().BeTrue();
        _mockChatMessageRepo.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageHandler_ShouldFail_WhenSendingToSelf()
    {
        var handler = new SendMessageHandler(_mockUow.Object, _mockSendLogger.Object);
        var senderId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = senderId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByParticipantsAsync(parking.Id, senderId, It.IsAny<CancellationToken>())).ReturnsAsync((Conversation)null);
        _mockConversationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Conversation>());

        var res = await handler.HandleAsync(new SendMessageCommand(senderId, new SendMessageDto(parking.Id, "Hello", null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Cannot start a conversation with yourself");
    }

    // MarkMessagesReadHandler Tests
    [Fact]
    public async Task MarkMessagesReadHandler_ShouldFail_WhenConversationNotFound()
    {
        var handler = new MarkMessagesReadHandler(_mockUow.Object);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conversation)null);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Conversation not found");
    }

    [Fact]
    public async Task MarkMessagesReadHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new MarkMessagesReadHandler(_mockUow.Object);
        var conversation = new Conversation { UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task MarkMessagesReadHandler_ShouldSucceed()
    {
        var handler = new MarkMessagesReadHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var conversation = new Conversation { Id = Guid.NewGuid(), UserId = userId, VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var res = await handler.HandleAsync(new MarkMessagesReadCommand(userId, conversation.Id));

        res.Success.Should().BeTrue();
        _mockChatMessageRepo.Verify(r => r.MarkAsReadAsync(conversation.Id, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
