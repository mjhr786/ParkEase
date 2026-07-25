using ParkingApp.Messaging.Contracts;
using ParkingApp.Messaging.Domain.Interfaces;

namespace ParkingApp.Messaging.Infrastructure.ModuleAdapters;

/// <summary>
/// Messaging-side cascade for user account deletion.
/// Does not call SaveChanges — Identity DeleteUser owns the transaction.
/// </summary>
internal sealed class MessagingUserDataCleanup : IMessagingUserDataCleanup
{
    private readonly IMessagingUnitOfWork _unitOfWork;

    public MessagingUserDataCleanup(IMessagingUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task StageDeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId, cancellationToken);
        _unitOfWork.Notifications.HardDeleteRange(notifications);

        var conversations = await _unitOfWork.Conversations.FindAsync(
            c => c.UserId == userId || c.VendorId == userId,
            cancellationToken);

        foreach (var conversation in conversations)
        {
            var messages = await _unitOfWork.ChatMessages.FindAsync(
                m => m.ConversationId == conversation.Id,
                cancellationToken);
            _unitOfWork.ChatMessages.HardDeleteRange(messages);
        }

        _unitOfWork.Conversations.HardDeleteRange(conversations);
    }
}
