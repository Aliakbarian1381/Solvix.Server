using Solvix.Server.Application.DTOs;
using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IChatService
    {
        Task<List<ChatDto>> GetUserChatsAsync(long userId);
        Task<ChatDto?> GetChatByIdAsync(Guid chatId, long userId);
        Task<(Guid chatId, bool alreadyExists)> StartChatWithUserAsync(long initiatorUserId, long recipientUserId);
        Task<Message> SaveMessageAsync(Guid chatId, long senderId, string content);
        Task BroadcastMessageAsync(Message message);
        Task<List<MessageDto>> GetChatMessagesAsync(Guid chatId, long userId, int skip = 0, int take = 50);
        Task MarkMessageAsReadAsync(int messageId, long readerUserId);
        Task MarkMultipleMessagesAsReadAsync(List<int> messageIds, long readerUserId);
        Task<bool> IsUserParticipantAsync(Guid chatId, long userId);
        Task<Message?> EditMessageAsync(int messageId, string newContent, long editorUserId);
        Task<Message?> DeleteMessageAsync(int messageId, long deleterUserId);
        Task BroadcastMessageUpdateAsync(Message message);
    }
}
