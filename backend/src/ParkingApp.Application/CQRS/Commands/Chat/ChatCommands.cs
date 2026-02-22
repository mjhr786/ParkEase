using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Chat;

// ────────────────────────────────────────────────────────────────
// Commands
// ────────────────────────────────────────────────────────────────

public sealed record SendMessageCommand(Guid SenderId, SendMessageDto Dto) : ICommand<ApiResponse<ChatMessageDto>>;
public sealed record MarkMessagesReadCommand(Guid UserId, Guid ConversationId) : ICommand<ApiResponse<bool>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class SendMessageHandler : ICommandHandler<SendMessageCommand, ApiResponse<ChatMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(IUnitOfWork unitOfWork, ILogger<SendMessageHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<ChatMessageDto>> HandleAsync(SendMessageCommand command, CancellationToken cancellationToken = default)
    {
        // Validate content
        var content = command.Dto.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return new ApiResponse<ChatMessageDto>(false, "Message content cannot be empty", null);

        if (content.Length > 2000)
            return new ApiResponse<ChatMessageDto>(false, "Message content cannot exceed 2000 characters", null);

        // Validate parking space exists
        var parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, cancellationToken);
        if (parkingSpace == null)
            return new ApiResponse<ChatMessageDto>(false, "Parking space not found", null);

        Conversation? conversation = null;

        // If a specific conversation is specified
        if (command.Dto.ConversationId.HasValue)
        {
            conversation = await _unitOfWork.Conversations.GetByIdAsync(command.Dto.ConversationId.Value, cancellationToken);
            
            // Validate participant
            if (conversation != null && conversation.UserId != command.SenderId && conversation.VendorId != command.SenderId)
                return new ApiResponse<ChatMessageDto>(false, "Unauthorized for this conversation", null);
        }

        // Fallback to searching if no specific conversation provided
        if (conversation == null)
        {
            conversation = await _unitOfWork.Conversations.GetByParticipantsAsync(
                command.Dto.ParkingSpaceId, command.SenderId, cancellationToken);

            // If sender is the vendor, try to find the conversation where they are the vendor
            if (conversation == null)
            {
                var vendorConversations = await _unitOfWork.Conversations.FindAsync(
                    c => c.ParkingSpaceId == command.Dto.ParkingSpaceId && c.VendorId == command.SenderId,
                    cancellationToken);
                
                // Only assume the conversation if there's exactly one
                var conversationsList = vendorConversations.ToList();
                if (conversationsList.Count == 1)
                {
                    conversation = conversationsList.First();
                }
            }
        }

        if (conversation == null)
        {
            // Only prevent self-chat when creating a NEW conversation
            if (parkingSpace.OwnerId == command.SenderId)
                return new ApiResponse<ChatMessageDto>(false, "Cannot start a conversation with yourself", null);

            conversation = new Conversation
            {
                ParkingSpaceId = command.Dto.ParkingSpaceId,
                UserId = command.SenderId,
                VendorId = parkingSpace.OwnerId
            };
            await _unitOfWork.Conversations.AddAsync(conversation, cancellationToken);
        }

        // Create message
        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderId = command.SenderId,
            Content = content
        };

        await _unitOfWork.ChatMessages.AddAsync(message, cancellationToken);

        // Update conversation metadata (change tracking detects this automatically)
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = content.Length > 100 ? content[..100] : content;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Load sender for the DTO
        var sender = await _unitOfWork.Users.GetByIdAsync(command.SenderId, cancellationToken);
        message.Sender = sender!;

        _logger.LogInformation("Message sent in conversation {ConversationId} by user {SenderId}",
            conversation.Id, command.SenderId);

        return new ApiResponse<ChatMessageDto>(true, "Message sent", message.ToDto());
    }
}

public sealed class MarkMessagesReadHandler : ICommandHandler<MarkMessagesReadCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public MarkMessagesReadHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<bool>> HandleAsync(MarkMessagesReadCommand command, CancellationToken cancellationToken = default)
    {
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(command.ConversationId, cancellationToken);
        if (conversation == null)
            return new ApiResponse<bool>(false, "Conversation not found", false);

        // Verify user is a participant
        if (conversation.UserId != command.UserId && conversation.VendorId != command.UserId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        await _unitOfWork.ChatMessages.MarkAsReadAsync(command.ConversationId, command.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool>(true, "Messages marked as read", true);
    }
}
