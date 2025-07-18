using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IMessageRepository : IRepository<Message>
    {
        Task<List<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50);
        Task<int> GetUnreadMessageCountAsync(Guid chatId, long userId);
        Task<List<Message>> GetUnreadMessagesAsync(Guid chatId, long userId);
        Task MarkAsReadAsync(int messageId, long userId);
        Task MarkMultipleAsReadAsync(List<int> messageIds, long userId);
        Task DeleteAllMessagesAsync(Guid chatId);
    }
}