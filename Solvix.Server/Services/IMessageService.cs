using Solvix.Server.Models;


namespace Solvix.Server.Services
{

    public interface IMessageService
    {
        Task<Message> SaveMessage(long senderId, long recipientId, string content);
        Task<List<Message>> GetUnreadMessagesForUser(long userId); 
        Task MarkMessagesAsRead(long senderId, long recipientId); 
        Task<Message> GetMessageById(int id);
        Task<List<Message>> GetChatHistory(long userId, long otherUserId);
    }
}
