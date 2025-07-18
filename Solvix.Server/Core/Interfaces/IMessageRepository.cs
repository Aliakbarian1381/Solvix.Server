using Solvix.Server.Core.Entities;

namespace Solvix.Server.Core.Interfaces
{
    public interface IMessageRepository : IRepository<Message>
    {
        Task<List<Message>> GetChatMessagesAsync(Guid chatId, int skip = 0, int take = 50);
        Task<Message?> GetByIdWithDetailsAsync(int messageId);
        Task MarkAsReadAsync(int messageId, long readerId);
        Task MarkMultipleAsReadAsync(List<int> messageIds, long readerId);
        Task<int> GetUnreadCountAsync(Guid chatId, long userId);
        Task DeleteAllMessagesAsync(Guid chatId);
    }
}