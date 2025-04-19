using Solvix.Server.Models;

namespace Solvix.Server.Services
{
    public interface IChatService
    {
        Task<Message> SaveMessageAsync(Guid chatId, long senderId, string content);
        Task BroadcastMessageAsync(Message message);
        // Optional: Add method for marking message as read if that logic is moved here
        Task MarkMessageAsReadAsync(int messageId, long readerUserId);
        Task<bool> IsUserParticipantAsync(Guid chatId, long userId);
        Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId);


    }
}
