using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.CQRS.Queries.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using System.Linq.Expressions;

namespace ParkingApp.UnitTests;

public class ChatTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConversationRepository> _mockConversationRepo;
    private readonly Mock<IChatMessageRepository> _mockMessageRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ILogger<SendMessageHandler>> _mockLogger;

    public ChatTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConversationRepo = new Mock<IConversationRepository>();
        _mockMessageRepo = new Mock<IChatMessageRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<SendMessageHandler>>();

        _mockUnitOfWork.Setup(u => u.Conversations).Returns(_mockConversationRepo.Object);
        _mockUnitOfWork.Setup(u => u.ChatMessages).Returns(_mockMessageRepo.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepo.Object);
    }

    // ── SendMessageHandler Tests ──

    [Fact]
    public async Task SendMessage_WhenContentEmpty_ShouldReturnFailure()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        var dto = new SendMessageDto(Guid.NewGuid(), "   ");
        var result = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Message content cannot be empty");
    }

    [Fact]
    public async Task SendMessage_WhenContentTooLong_ShouldReturnFailure()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        var dto = new SendMessageDto(Guid.NewGuid(), new string('x', 2001));
        var result = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Message content cannot exceed 2000 characters");
    }

    [Fact]
    public async Task SendMessage_WhenParkingNotFound_ShouldReturnFailure()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var dto = new SendMessageDto(Guid.NewGuid(), "Hello!");
        var result = await handler.HandleAsync(new SendMessageCommand(Guid.NewGuid(), dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }

    [Fact]
    public async Task SendMessage_WhenSenderIsOwner_ShouldReturnFailure()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        var userId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = userId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);

        var dto = new SendMessageDto(parkingId, "Hello!");
        var result = await handler.HandleAsync(new SendMessageCommand(userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Cannot start a conversation with yourself");
    }

    [Fact]
    public async Task SendMessage_WhenValid_ShouldCreateConversationAndMessage()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        var senderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        var sender = new User { Id = senderId, FirstName = "John", LastName = "Doe" };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByParticipantsAsync(parkingId, senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);
        _mockConversationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Conversation>());
        _mockUserRepo.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sender);

        var dto = new SendMessageDto(parkingId, "Hello, is this spot available?");
        var result = await handler.HandleAsync(new SendMessageCommand(senderId, dto));

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Message sent");
        result.Data.Should().NotBeNull();
        result.Data!.Content.Should().Be("Hello, is this spot available?");
        result.Data.SenderName.Should().Be("John Doe");

        _mockConversationRepo.Verify(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMessageRepo.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WhenConversationExists_ShouldReuseIt()
    {
        var handler = new SendMessageHandler(_mockUnitOfWork.Object, _mockLogger.Object);
        var senderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        var sender = new User { Id = senderId, FirstName = "Jane", LastName = "Smith" };
        var existingConversation = new Conversation { Id = convId, ParkingSpaceId = parkingId, UserId = senderId, VendorId = ownerId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        _mockConversationRepo.Setup(r => r.GetByParticipantsAsync(parkingId, senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConversation);
        _mockUserRepo.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sender);

        var dto = new SendMessageDto(parkingId, "Follow up question");
        var result = await handler.HandleAsync(new SendMessageCommand(senderId, dto));

        result.Success.Should().BeTrue();
        result.Data!.ConversationId.Should().Be(convId);

        // Should NOT add a new conversation
        _mockConversationRepo.Verify(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── MarkMessagesReadHandler Tests ──

    [Fact]
    public async Task MarkMessagesRead_WhenConversationNotFound_ShouldReturnFailure()
    {
        var handler = new MarkMessagesReadHandler(_mockUnitOfWork.Object);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Conversation not found");
    }

    [Fact]
    public async Task MarkMessagesRead_WhenUserNotParticipant_ShouldReturnFailure()
    {
        var handler = new MarkMessagesReadHandler(_mockUnitOfWork.Object);
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var result = await handler.HandleAsync(new MarkMessagesReadCommand(Guid.NewGuid(), convId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task MarkMessagesRead_WhenValid_ShouldSucceed()
    {
        var handler = new MarkMessagesReadHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, UserId = userId, VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var result = await handler.HandleAsync(new MarkMessagesReadCommand(userId, convId));

        result.Success.Should().BeTrue();
        _mockMessageRepo.Verify(r => r.MarkAsReadAsync(convId, userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetConversationsHandler Tests ──

    [Fact]
    public async Task GetConversations_WhenValid_ShouldReturnConversationsList()
    {
        var handler = new GetConversationsHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();

        var conversation = new Conversation
        {
            Id = convId,
            ParkingSpaceId = parkingId,
            UserId = userId,
            VendorId = Guid.NewGuid(),
            LastMessageAt = DateTime.UtcNow,
            LastMessagePreview = "Test preview",
            ParkingSpace = new ParkingSpace { Id = parkingId, Title = "Test Space" },
            Vendor = new User { Id = Guid.NewGuid(), FirstName = "Vendor", LastName = "Name" },
            User = new User { Id = userId, FirstName = "User", LastName = "Name" }
        };

        _mockConversationRepo.Setup(r => r.GetByUserIdAsync(userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation> { conversation });
        _mockConversationRepo.Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockMessageRepo.Setup(r => r.GetUnreadCountByConversationAsync(convId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await handler.HandleAsync(new GetConversationsQuery(userId, 1, 20));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Conversations.Should().HaveCount(1);
        result.Data.Conversations.First().UnreadCount.Should().Be(2);
        result.Data.Conversations.First().ParkingSpaceTitle.Should().Be("Test Space");
        result.Data.Conversations.First().OtherParticipantName.Should().Be("Vendor Name");
    }

    // ── GetMessagesHandler Tests ──

    [Fact]
    public async Task GetMessages_WhenConversationNotFound_ShouldReturnFailure()
    {
        var handler = new GetMessagesHandler(_mockUnitOfWork.Object);
        _mockConversationRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var result = await handler.HandleAsync(new GetMessagesQuery(Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Conversation not found");
    }

    [Fact]
    public async Task GetMessages_WhenUserNotParticipant_ShouldReturnUnauthorized()
    {
        var handler = new GetMessagesHandler(_mockUnitOfWork.Object);
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, UserId = Guid.NewGuid(), VendorId = Guid.NewGuid() };
        _mockConversationRepo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var result = await handler.HandleAsync(new GetMessagesQuery(Guid.NewGuid(), convId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetMessages_WhenValid_ShouldReturnMessages()
    {
        var handler = new GetMessagesHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var conversation = new Conversation { Id = convId, UserId = userId, VendorId = Guid.NewGuid() };
        var sender = new User { Id = userId, FirstName = "Test", LastName = "User" };
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Id = Guid.NewGuid(), ConversationId = convId, SenderId = userId, Content = "Hello", Sender = sender, CreatedAt = DateTime.UtcNow }
        };

        _mockConversationRepo.Setup(r => r.GetByIdAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        _mockMessageRepo.Setup(r => r.GetByConversationIdAsync(convId, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var result = await handler.HandleAsync(new GetMessagesQuery(userId, convId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Content.Should().Be("Hello");
    }

    // ── Domain Entity Tests ──

    [Fact]
    public void ChatMessage_ShouldInitializeWithDefaults()
    {
        var message = new ChatMessage();

        message.Content.Should().BeEmpty();
        message.IsRead.Should().BeFalse();
        message.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Conversation_ShouldInitializeWithDefaults()
    {
        var conversation = new Conversation();

        conversation.LastMessageAt.Should().BeNull();
        conversation.LastMessagePreview.Should().BeNull();
        conversation.Messages.Should().BeEmpty();
    }
}
