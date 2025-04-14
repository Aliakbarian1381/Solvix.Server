using Solvix.Server.Models;


namespace Solvix.Server.Services
{
    public interface IMessageService
    {
        Task<Message> SaveMessage(int senderId, int recipientId, string content);
        Task<List<Message>> GetUnreadMessagesForUser(int userId);
        Task MarkMessageAsRead(int messageId);

    }
}
